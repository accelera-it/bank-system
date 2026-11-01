namespace CentralApi.IntegrationTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using BankSystem.Common.Utils;
    using BankSystem.Common.Utils.Models;
    using BankSystem.Tests.Shared;
    using FluentAssertions;
    using Newtonsoft.Json;
    using Xunit;

    public class ReceiveTransactionsApiTests : IClassFixture<CentralApiFactory>
    {
        private const string Url = "/api/ReceiveTransactions";

        private readonly CentralApiFactory factory;
        private readonly HttpClient client;

        public ReceiveTransactionsApiTests(CentralApiFactory factory)
        {
            this.factory = factory;
            this.client = factory.CreateClient();
            factory.EnsureTestBankAsync().GetAwaiter().GetResult();
        }

        private static object Transaction(string destinationBank = CentralApiFactory.BankName)
            => new
            {
                Description = "Global transfer",
                Amount = 75m,
                SenderName = "Sender",
                RecipientName = "Recipient",
                SenderAccountUniqueId = "TST00000001",
                DestinationBankSwiftCode = CentralApiFactory.BankSwiftCode,
                DestinationBankName = destinationBank,
                DestinationBankCountry = CentralApiFactory.BankCountry,
                DestinationBankAccountUniqueId = "TST00000002",
                ReferenceNumber = "REF123"
            };

        private Task<HttpResponseMessage> PostAsync(object model, string senderName = CentralApiFactory.BankName)
        {
            var payload = SecureRequestBuilder.CreatePayload(
                model, this.factory.BankKeys.PrivateKey, this.factory.CentralApiKeys.PublicKey);

            var body = SecureRequestBuilder.ToRequestBody(payload, new Dictionary<string, object>
            {
                ["BankName"] = senderName,
                ["BankSwiftCode"] = CentralApiFactory.BankSwiftCode,
                ["BankCountry"] = CentralApiFactory.BankCountry
            });

            return this.client.PostAsync(Url, new StringContent(
                JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));
        }

        [Fact]
        public async Task Post_ToKnownBank_ShouldForwardSignedAndEncryptedTransfer()
        {
            this.factory.BankServer.ResponseStatus = HttpStatusCode.OK;

            var response = await this.PostAsync(Transaction());

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var forwarded = JsonConvert.DeserializeObject<string>(this.factory.BankServer.Requests.Last());
            var payload = JsonConvert.DeserializeObject<SecurePayload>(
                Encoding.UTF8.GetString(System.Convert.FromBase64String(forwarded)));
            var decrypted = SignatureVerificationUtil.DecryptDataAndVerifySignature(new SignatureVerificationModel
            {
                DecryptionPrivateKey = this.factory.BankKeys.PrivateKey,
                SignaturePublicKey = this.factory.CentralApiKeys.PublicKey,
                EncryptedKey = payload.EncryptedKey,
                EncryptedIv = payload.EncryptedIv,
                Data = payload.Data,
                Signature = payload.Signature
            });

            decrypted.Should().Contain("REF123");
        }

        [Fact]
        public async Task Post_WhenDestinationBankRefuses_ShouldReturnBadRequest()
        {
            this.factory.BankServer.ResponseStatus = HttpStatusCode.BadRequest;

            try
            {
                var response = await this.PostAsync(Transaction());

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            }
            finally
            {
                this.factory.BankServer.ResponseStatus = HttpStatusCode.OK;
            }
        }

        [Fact]
        public async Task Post_WhenDestinationBankFails_ShouldReturnBadGateway()
        {
            this.factory.BankServer.ResponseStatus = HttpStatusCode.InternalServerError;

            try
            {
                var response = await this.PostAsync(Transaction());

                response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
            }
            finally
            {
                this.factory.BankServer.ResponseStatus = HttpStatusCode.OK;
            }
        }

        [Fact]
        public async Task Post_ToUnknownDestinationBank_ShouldReturnNotFound()
        {
            var response = await this.PostAsync(Transaction("Missing bank"));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Post_FromUnknownBank_ShouldReturnForbidden()
        {
            var response = await this.PostAsync(Transaction(), "Unknown sender");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Post_WithGarbageBody_ShouldReturnForbidden()
        {
            var response = await this.client.PostAsync(Url, new StringContent(
                JsonConvert.SerializeObject("not-base64"), Encoding.UTF8, "application/json"));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
