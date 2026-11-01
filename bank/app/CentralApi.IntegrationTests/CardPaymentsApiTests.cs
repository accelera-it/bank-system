namespace CentralApi.IntegrationTests
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    public class CardPaymentsApiTests : IClassFixture<CentralApiFactory>
    {
        private const string Url = "/api/CardPayments";

        private readonly CentralApiFactory factory;
        private readonly HttpClient client;

        public CardPaymentsApiTests(CentralApiFactory factory)
        {
            this.factory = factory;
            this.client = factory.CreateClient();
            factory.EnsureTestBankAsync().GetAwaiter().GetResult();
        }

        private static object Payment(string number, string securityCode = "123")
            => new
            {
                Amount = 10m,
                Description = "Purchase",
                Number = number,
                Name = "Card Owner",
                RecipientName = "Shop",
                ExpiryDate = "12/30",
                SecurityCode = securityCode,
                DestinationBankName = CentralApiFactory.BankName,
                DestinationBankSwiftCode = CentralApiFactory.BankSwiftCode,
                DestinationBankCountry = CentralApiFactory.BankCountry,
                DestinationBankAccountUniqueId = "TST00000002"
            };

        [Fact]
        public async Task Post_WithKnownCardPrefix_ShouldForwardToIssuingBank()
        {
            this.factory.BankServer.ResponseStatus = HttpStatusCode.OK;

            var response = await this.client.PostAsJsonAsync(
                Url, Payment(CentralApiFactory.BankCardPrefix + "1234567890123"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            this.factory.BankServer.Requests.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Post_WhenIssuingBankRefuses_ShouldReturnBadRequest()
        {
            this.factory.BankServer.ResponseStatus = HttpStatusCode.BadRequest;

            try
            {
                var response = await this.client.PostAsJsonAsync(
                    Url, Payment(CentralApiFactory.BankCardPrefix + "1234567890123"));

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            }
            finally
            {
                this.factory.BankServer.ResponseStatus = HttpStatusCode.OK;
            }
        }

        [Fact]
        public async Task Post_WithUnknownCardPrefix_ShouldReturnBadRequest()
        {
            var response = await this.client.PostAsJsonAsync(Url, Payment("9991234567890123"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Post_WithInvalidModel_ShouldReturnBadRequest()
        {
            var response = await this.client.PostAsJsonAsync(
                Url, Payment(CentralApiFactory.BankCardPrefix + "1234567890123", securityCode: "1"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
