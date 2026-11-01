namespace BankSystem.IntegrationTests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using BankSystem.Tests.Shared;
    using FluentAssertions;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    public class ReceiveMoneyTransfersApiTests : IClassFixture<BankWebFactory>
    {
        private const string Url = "/api/ReceiveMoneyTransfers";

        private readonly BankWebFactory factory;
        private readonly HttpClient client;

        public ReceiveMoneyTransfersApiTests(BankWebFactory factory)
        {
            this.factory = factory;
            this.client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        private static object Transfer(
            decimal amount = 100,
            string account = BankWebFactory.SeededAccountUniqueId,
            string recipient = BankWebFactory.SeededUserFullName,
            string referenceNumber = null)
            => new
            {
                Description = "Integration transfer",
                Amount = amount,
                DestinationBankAccountUniqueId = account,
                SenderName = "Sender",
                RecipientName = recipient,
                SenderAccountUniqueId = "XYZ00000001",
                ReferenceNumber = referenceNumber ?? Guid.NewGuid().ToString("N").Substring(0, 20)
            };

        private Task<HttpResponseMessage> PostAsync(object model, string signedWith = null, DateTime? timestamp = null)
        {
            var payload = SecureRequestBuilder.CreatePayload(
                model,
                signedWith ?? this.factory.CentralApiKeys.PrivateKey,
                this.factory.BankKeys.PublicKey,
                timestamp);

            return this.client.PostAsync(Url, ApiRequests.JsonBody(SecureRequestBuilder.ToRequestBody(payload)));
        }

        [Fact]
        public async Task Post_WithValidRequest_ShouldCreditAccountAndRecordTransfer()
        {
            var before = (await this.factory.GetSeededAccountAsync()).Balance;
            var transfer = Transfer(amount: 250);

            var response = await this.PostAsync(transfer);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await this.factory.GetSeededAccountAsync()).Balance.Should().Be(before + 250);

            var reference = (string)transfer.GetType().GetProperty("ReferenceNumber").GetValue(transfer);
            var recorded = await this.factory.WithDbAsync(
                db => db.Transfers.AnyAsync(t => t.ReferenceNumber == reference));
            recorded.Should().BeTrue();
        }

        [Fact]
        public async Task Post_DeliveredTwice_ShouldCreditOnceAndSucceedBothTimes()
        {
            var before = (await this.factory.GetSeededAccountAsync()).Balance;
            var transfer = Transfer(amount: 25);

            var first = await this.PostAsync(transfer);
            var second = await this.PostAsync(transfer);

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            (await this.factory.GetSeededAccountAsync()).Balance.Should().Be(before + 25);
        }

        [Fact]
        public async Task Post_SameReferenceWithOtherAmount_ShouldNotCredit()
        {
            var reference = Guid.NewGuid().ToString("N").Substring(0, 20);
            await this.PostAsync(Transfer(amount: 25, referenceNumber: reference));
            var before = (await this.factory.GetSeededAccountAsync()).Balance;

            var response = await this.PostAsync(Transfer(amount: 40, referenceNumber: reference));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await this.factory.GetSeededAccountAsync()).Balance.Should().Be(before);
        }

        [Fact]
        public async Task Post_SignedWithUnknownKey_ShouldReturnForbiddenAndNotCredit()
        {
            var before = (await this.factory.GetSeededAccountAsync()).Balance;
            var attacker = SecureRequestBuilder.GenerateKeyPair();

            var response = await this.PostAsync(Transfer(), attacker.PrivateKey);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await this.factory.GetSeededAccountAsync()).Balance.Should().Be(before);
        }

        [Fact]
        public async Task Post_WithExpiredTimestamp_ShouldReturnForbidden()
        {
            var response = await this.PostAsync(Transfer(), timestamp: DateTime.UtcNow.AddMinutes(-10));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Post_WithGarbageBody_ShouldReturnForbidden()
        {
            var response = await this.client.PostAsync(Url, ApiRequests.JsonBody("not-base64"));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Post_ToUnknownAccount_ShouldReturnBadRequest()
        {
            var response = await this.PostAsync(Transfer(account: "MISSING00000"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Post_WithWrongRecipientName_ShouldReturnBadRequest()
        {
            var response = await this.PostAsync(Transfer(recipient: "Somebody Else"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
