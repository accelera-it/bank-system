namespace BankSystem.Web.Infrastructure.Helpers.GlobalTransferHelpers
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using MapsterMapper;
    using Common.Configuration;
    using Common.Utils;
    using Messaging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Models;
    using Newtonsoft.Json;
    using Services.BankAccount;
    using Services.Models.BankAccount;
    using Services.Models.MoneyTransfer;
    using Services.MoneyTransfer;

    public class GlobalTransferHelper : IGlobalTransferHelper
    {
        private enum DeliveryOutcome
        {
            Accepted,
            Refused,
            Unknown
        }

        private const string CentralApiTransferSubmitUrlFormat = "{0}api/ReceiveTransactions";

        // Every 30 seconds through the queue, so a transfer gets a day of retries before it needs a person
        public const int MaxDeliveryAttempts = 2880;

        // Delivering again is safe, so a brief failure need not leave the customer with an open question
        private const int RequestDeliveryAttempts = 3;
        private static readonly TimeSpan RequestRetryDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan RequestRetryWindow = TimeSpan.FromSeconds(20);

        private const int RetryBatchSize = 50;

        private readonly IBankAccountService bankAccountService;
        private readonly BankConfiguration bankConfiguration;
        private readonly IMoneyTransferService moneyTransferService;
        private readonly IMapper mapper;
        private readonly ILogger<GlobalTransferHelper> logger;
        private readonly IPendingTransferQueue queue;

        public GlobalTransferHelper(
            IBankAccountService bankAccountService,
            IMoneyTransferService moneyTransferService,
            IOptions<BankConfiguration> bankConfigurationOptions,
            IMapper mapper,
            ILogger<GlobalTransferHelper> logger,
            IPendingTransferQueue queue)
        {
            this.queue = queue;
            this.bankAccountService = bankAccountService;
            this.moneyTransferService = moneyTransferService;
            this.mapper = mapper;
            this.logger = logger;
            this.bankConfiguration = bankConfigurationOptions.Value;
        }

        public async Task<GlobalTransferResult> TransferMoneyAsync(GlobalTransferDto model)
        {
            if (!ValidationUtil.IsObjectValid(model))
            {
                return GlobalTransferResult.GeneralFailure;
            }

            var account = await this.bankAccountService
                .GetByIdAsync<BankAccountConciseServiceModel>(model.SourceAccountId);

            // check if account exists and recipient name is accurate
            if (account == null)
            {
                return GlobalTransferResult.GeneralFailure;
            }

            var submitDto = this.mapper.Map<CentralApiSubmitTransferDto>(model);
            submitDto.SenderName = account.UserFullName;
            submitDto.SenderAccountUniqueId = account.UniqueId;
            submitDto.ReferenceNumber = model.ReferenceNumber ?? ReferenceNumberGenerator.GenerateReferenceNumber();

            // remove money from source account first: it cannot be spent twice while CentralApi works,
            // and the pending row saved with it means a crash cannot lose the transfer
            var serviceModel = new MoneyTransferCreateServiceModel
            {
                Amount = -model.Amount,
                Source = account.UniqueId,
                Description = model.Description,
                AccountId = account.Id,
                DestinationBankAccountUniqueId = model.DestinationBankAccountUniqueId,
                SenderName = account.UserFullName,
                RecipientName = model.RecipientName,
                ReferenceNumber = submitDto.ReferenceNumber
            };

            var withdrawal = await this.moneyTransferService.WithdrawAsync(
                serviceModel, JsonConvert.SerializeObject(submitDto));
            if (withdrawal != MoneyTransferResult.Succeeded)
            {
                return withdrawal == MoneyTransferResult.InsufficientFunds
                    ? GlobalTransferResult.InsufficientFunds
                    : GlobalTransferResult.GeneralFailure;
            }

            // contact the CentralApi to execute the transfer
            switch (await this.DeliverWithinRequestAsync(submitDto))
            {
                case DeliveryOutcome.Accepted:
                    await this.FinishSafelyAsync(submitDto.ReferenceNumber, DeliveryOutcome.Accepted);
                    return GlobalTransferResult.Succeeded;
                case DeliveryOutcome.Refused:
                    await this.FinishSafelyAsync(submitDto.ReferenceNumber, DeliveryOutcome.Refused);
                    return GlobalTransferResult.GeneralFailure;
                default:
                    await this.queue.EnqueueAsync(submitDto.ReferenceNumber);
                    return GlobalTransferResult.Pending;
            }
        }

        private async Task<DeliveryOutcome> DeliverWithinRequestAsync(CentralApiSubmitTransferDto model)
        {
            var started = Stopwatch.StartNew();

            for (var attempt = 1; ; attempt++)
            {
                var outcome = await this.ContactCentralApiAsync(model);
                if (outcome != DeliveryOutcome.Unknown
                    || attempt == RequestDeliveryAttempts
                    || started.Elapsed > RequestRetryWindow)
                {
                    return outcome;
                }

                await Task.Delay(RequestRetryDelay);
            }
        }

        public async Task RetryPendingTransfersAsync(TimeSpan minimumAge, TimeSpan attemptGap)
        {
            var now = DateTime.UtcNow;
            var pendingTransfers = await this.moneyTransferService.GetPendingTransfersAsync(
                now - minimumAge, now - attemptGap, MaxDeliveryAttempts, RetryBatchSize);

            foreach (var pending in pendingTransfers)
            {
                await this.DeliverAsync(pending);
            }
        }

        public async Task<bool> DeliverPendingTransferAsync(string referenceNumber)
        {
            var pending = await this.moneyTransferService.GetPendingTransferAsync(referenceNumber, MaxDeliveryAttempts);

            return pending != null && await this.DeliverAsync(pending);
        }

        private async Task<bool> DeliverAsync(PendingTransferServiceModel pending)
        {
            var model = JsonConvert.DeserializeObject<CentralApiSubmitTransferDto>(pending.Payload);
            var outcome = await this.ContactCentralApiAsync(model);
            if (outcome != DeliveryOutcome.Unknown)
            {
                await this.FinishSafelyAsync(pending.ReferenceNumber, outcome);
                return false;
            }

            var attempts = await this.moneyTransferService.AddPendingTransferAttemptAsync(pending.ReferenceNumber);
            if (attempts < MaxDeliveryAttempts)
            {
                return true;
            }

            this.logger.LogCritical(
                "Transfer {ReferenceNumber} still has no answer after {Attempts} attempts: it needs a person",
                pending.ReferenceNumber, attempts);

            return false;
        }

        // The pending row survives a failure here, so the retry service finishes the job
        private async Task FinishSafelyAsync(string referenceNumber, DeliveryOutcome outcome)
        {
            try
            {
                if (outcome == DeliveryOutcome.Accepted)
                {
                    await this.moneyTransferService.CompletePendingTransferAsync(referenceNumber);
                }
                else if (!await this.moneyTransferService.RefundPendingTransferAsync(referenceNumber))
                {
                    this.logger.LogError("Refund of transfer {ReferenceNumber} will be retried", referenceNumber);
                    return;
                }

                await this.queue.NotifySettledAsync(referenceNumber);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Finishing transfer {ReferenceNumber} will be retried", referenceNumber);
            }
        }

        private async Task<DeliveryOutcome> ContactCentralApiAsync(CentralApiSubmitTransferDto model)
        {
            var encryptedData = this.SignAndEncryptData(model);

            try
            {
                var client = new HttpClient();
                var response = await client.PostAsJsonAsync(
                    string.Format(CentralApiTransferSubmitUrlFormat, this.bankConfiguration.CentralApiAddress),
                    encryptedData);

                if (response.IsSuccessStatusCode)
                {
                    return DeliveryOutcome.Accepted;
                }

                // Gateway and server errors say nothing about whether the other bank got the money
                var status = (int)response.StatusCode;
                return status >= 500 || status == 408 || status == 429
                    ? DeliveryOutcome.Unknown
                    : DeliveryOutcome.Refused;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                this.logger.LogError(ex, "CentralApi did not answer for transfer {ReferenceNumber}", model.ReferenceNumber);

                return DeliveryOutcome.Unknown;
            }
        }

        private string SignAndEncryptData(CentralApiSubmitTransferDto model)
        {
            using var rsa = RSA.Create();
            RsaExtensions.FromXmlString(rsa, this.bankConfiguration.Key);
            var aesParams = CryptographyExtensions.GenerateKey();
            var key = Convert.FromBase64String(aesParams[0]);
            var iv = Convert.FromBase64String(aesParams[1]);

            var serializedModel = JsonConvert.SerializeObject(model);
            var dataObject = new
            {
                Model = serializedModel,
                Timestamp = DateTime.UtcNow
            };

            var data = JsonConvert.SerializeObject(dataObject);

            var signature = Convert.ToBase64String(rsa
                .SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

            string encryptedKey;
            string encryptedIv;
            using (var encryptionRsa = RSA.Create())
            {
                RsaExtensions.FromXmlString(encryptionRsa, this.bankConfiguration.CentralApiPublicKey);
                encryptedKey = Convert.ToBase64String(encryptionRsa.Encrypt(key, RSAEncryptionPadding.Pkcs1));
                encryptedIv = Convert.ToBase64String(encryptionRsa.Encrypt(iv, RSAEncryptionPadding.Pkcs1));
            }

            var encryptedData = Convert.ToBase64String(CryptographyExtensions.Encrypt(data, key, iv));

            var json = new
            {
                BankName = this.bankConfiguration.BankName,
                BankSwiftCode = this.bankConfiguration.UniqueIdentifier,
                BankCountry = this.bankConfiguration.Country,
                EncryptedKey = encryptedKey,
                EncryptedIv = encryptedIv,
                Data = encryptedData,
                Signature = signature
            };

            var jsonRequest = JsonConvert.SerializeObject(json);
            var encryptedRequest = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonRequest));

            return encryptedRequest;
        }
    }
}