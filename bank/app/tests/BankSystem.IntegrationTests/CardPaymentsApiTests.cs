namespace BankSystem.IntegrationTests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using BankSystem.Models;
    using BankSystem.Tests.Shared;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    public class CardPaymentsApiTests : IClassFixture<BankWebFactory>
    {
        private const string Url = "/api/CardPayments";
        private const string CardNumber = "1012345678901234";
        private const string SecurityCode = "123";

        private static readonly string ValidExpiry =
            DateTime.UtcNow.AddYears(1).ToString("MM/yy", CultureInfo.InvariantCulture);

        private readonly BankWebFactory factory;
        private readonly HttpClient client;

        public CardPaymentsApiTests(BankWebFactory factory)
        {
            this.factory = factory;
            this.client = factory.CreateClient();
        }

        private object Payment(decimal amount = 50, string securityCode = SecurityCode, string number = CardNumber)
            => new
            {
                Amount = amount,
                Description = "Card purchase",
                Number = number,
                Name = BankWebFactory.SeededUserFullName,
                RecipientName = "Shop",
                ExpiryDate = ValidExpiry,
                SecurityCode = securityCode,
                DestinationBankName = "Other bank",
                DestinationBankSwiftCode = "XYZ",
                DestinationBankCountry = "Uzbekistan",
                DestinationBankAccountUniqueId = "XYZ00000001"
            };

        private async Task EnsureCardAsync()
        {
            var account = await this.factory.GetSeededAccountAsync();

            await this.factory.WithDbAsync(async db =>
            {
                if (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .AnyAsync(db.Cards, c => c.Number == CardNumber))
                {
                    return 0;
                }

                db.Cards.Add(new Card
                {
                    Number = CardNumber,
                    Name = BankWebFactory.SeededUserFullName,
                    ExpiryDate = ValidExpiry,
                    SecurityCode = SecurityCode,
                    UserId = account.UserId,
                    AccountId = account.Id
                });

                return await db.SaveChangesAsync();
            });
        }

        private Task<HttpResponseMessage> PostAsync(object model)
        {
            var payload = SecureRequestBuilder.CreatePayload(
                model, this.factory.CentralApiKeys.PrivateKey, this.factory.BankKeys.PublicKey);

            return this.client.PostAsync(Url, ApiRequests.JsonBody(SecureRequestBuilder.ToRequestBody(payload)));
        }

        [Fact]
        public async Task Post_WithValidCard_ShouldDebitAccountAndContactCentralApi()
        {
            await this.EnsureCardAsync();
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;
            var before = (await this.factory.GetSeededAccountAsync()).Balance;

            var response = await this.PostAsync(this.Payment(amount: 50));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            this.factory.CentralApi.Requests.Should().NotBeEmpty();
            (await this.factory.GetSeededAccountAsync()).Balance.Should().Be(before - 50);
        }

        [Fact]
        public async Task Post_WhenCentralApiRefuses_ShouldNotDebitAccount()
        {
            await this.EnsureCardAsync();
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.BadRequest;
            var before = (await this.factory.GetSeededAccountAsync()).Balance;

            try
            {
                var response = await this.PostAsync(this.Payment());

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                (await this.factory.GetSeededAccountAsync()).Balance.Should().Be(before);
            }
            finally
            {
                this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;
            }
        }

        [Fact]
        public async Task Post_WhenCentralApiRefuses_ShouldRecordWithdrawalAndRefund()
        {
            await this.EnsureCardAsync();
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.BadRequest;
            var accountId = (await this.factory.GetSeededAccountAsync()).Id;
            var before = await this.factory.WithDbAsync(db => db.Transfers.CountAsync(t => t.AccountId == accountId));

            try
            {
                await this.PostAsync(this.Payment(amount: 7));

                var transfers = await this.factory.WithDbAsync(db => db.Transfers
                    .Where(t => t.AccountId == accountId && (t.Amount == 7 || t.Amount == -7))
                    .ToListAsync());
                transfers.Should().HaveCount(2);
                transfers.Sum(t => t.Amount).Should().Be(0);
                (await this.factory.WithDbAsync(db => db.Transfers.CountAsync(t => t.AccountId == accountId)))
                    .Should().Be(before + 2);
            }
            finally
            {
                this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;
            }
        }

        [Fact]
        public async Task Post_WhenCentralApiIsDown_ShouldHoldMoneyAsPending()
        {
            using var factory = new BankWebFactory();
            var tests = new CardPaymentsApiTests(factory);
            await tests.EnsureCardAsync();
            factory.CentralApi.Dispose();
            var before = (await factory.GetSeededAccountAsync()).Balance;

            var response = await tests.PostAsync(tests.Payment());

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await factory.GetSeededAccountAsync()).Balance.Should().Be(before - 50);
            (await factory.WithDbAsync(db => db.PendingTransfers.CountAsync())).Should().Be(1);
        }

        [Fact]
        public async Task Post_WithInsufficientFunds_ShouldNotContactCentralApi()
        {
            await this.EnsureCardAsync();
            var balance = (await this.factory.GetSeededAccountAsync()).Balance;
            var requests = this.factory.CentralApi.Requests.Count;

            var response = await this.PostAsync(this.Payment(amount: balance + 1));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            this.factory.CentralApi.Requests.Should().HaveCount(requests);
            (await this.factory.GetSeededAccountAsync()).Balance.Should().Be(balance);
        }

        [Fact]
        public async Task Post_WithWrongSecurityCode_ShouldReturnBadRequest()
        {
            await this.EnsureCardAsync();

            var response = await this.PostAsync(this.Payment(securityCode: "999"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Post_WithInvalidModel_ShouldReturnBadRequest()
        {
            var response = await this.PostAsync(this.Payment(number: null));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Post_WithUnsignedBody_ShouldReturnForbidden()
        {
            var response = await this.client.PostAsync(Url, ApiRequests.JsonBody("not-base64"));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
