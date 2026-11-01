namespace CentralApi.Controllers
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    using Infrastructure.Helpers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using Models;
    using Services.Bank;
    using Services.Models.Banks;

    [ApiController]
    [Route("api/[controller]")]
    public class CardPaymentsController : ControllerBase
    {
        private const string NotFoundState = "NotFound";

        private readonly IBanksService banksService;
        private readonly CentralApiConfiguration configuration;

        public CardPaymentsController(IBanksService banksService, IOptions<CentralApiConfiguration> configuration)
        {
            this.banksService = banksService;
            this.configuration = configuration.Value;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CardPaymentDto model)
        {
            try
            {
                var first3Digits = model.Number.Substring(0, 3);
                var bank = await this.banksService
                    .GetBankByBankIdentificationCardNumbersAsync<BankPaymentServiceModel>(first3Digits);
                if (bank?.CardPaymentUrl == null)
                {
                    return this.BadRequest();
                }

                var encryptedAndSignedData = TransactionHelper.SignAndEncryptData(model, this.configuration.Key, bank.ApiKey);
                var client = new HttpClient();
                var request = await client.PostAsJsonAsync(bank.CardPaymentUrl, encryptedAndSignedData);

                if (request.StatusCode == HttpStatusCode.Accepted)
                {
                    return this.Accepted();
                }

                if ((int)request.StatusCode >= 500)
                {
                    return this.StatusCode((int)HttpStatusCode.BadGateway);
                }

                if (request.StatusCode != HttpStatusCode.OK)
                {
                    return this.BadRequest();
                }

                return this.Ok();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Not a refusal: the bank may have taken the money
                return this.StatusCode((int)HttpStatusCode.BadGateway);
            }
            catch
            {
                return this.BadRequest();
            }
        }

        // The merchant does not know the customer's bank for a direct payment, so every bank is asked.
        // The answer is only a state, and the reference number is the merchant's own random value.
        [HttpPost("status")]
        public async Task<IActionResult> Status([FromBody] PaymentStatusRequest model)
        {
            var banks = await this.banksService.GetAllBanksSupportingPaymentsAsync<BankPaymentServiceModel>();
            var unanswered = false;

            foreach (var bank in banks.Where(b => b.CardPaymentUrl != null))
            {
                var encryptedAndSignedData = TransactionHelper.SignAndEncryptData(model, this.configuration.Key, bank.ApiKey);

                try
                {
                    var client = new HttpClient();
                    var response = await client.PostAsJsonAsync($"{bank.CardPaymentUrl}/status", encryptedAndSignedData);
                    if (!response.IsSuccessStatusCode)
                    {
                        unanswered = true;
                        continue;
                    }

                    var answer = await response.Content.ReadFromJsonAsync<PaymentStatusResponse>();
                    if (answer?.State != NotFoundState)
                    {
                        return this.Ok(answer);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    unanswered = true;
                }
            }

            return unanswered
                ? this.StatusCode((int)HttpStatusCode.BadGateway)
                : this.Ok(new PaymentStatusResponse { State = NotFoundState });
        }
    }
}