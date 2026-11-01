namespace BankSystem.IntegrationTests
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using BankSystem.Tests.Shared;
    using Common;
    using Common.Configuration;
    using FluentAssertions;
    using MapsterMapper;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Newtonsoft.Json;
    using Services.BankAccount;
    using Services.Models.BankAccount;
    using Services.Models.MoneyTransfer;
    using Services.MoneyTransfer;
    using Web.Infrastructure.Helpers.GlobalTransferHelpers;
    using Web.Infrastructure.Messaging;
    using Web.Infrastructure.Helpers.GlobalTransferHelpers.Models;
    using Xunit;

    public class GlobalTransferDeliveryTests : IClassFixture<BankWebFactory>
    {
        private const string Url = "/administration/pendingtransfers";

        private readonly BankWebFactory factory;
        private readonly Mock<IPendingTransferQueue> queue = new Mock<IPendingTransferQueue>();

        public GlobalTransferDeliveryTests(BankWebFactory factory) => this.factory = factory;

        private static GlobalTransferDto Transfer(string accountId, decimal amount)
            => new GlobalTransferDto
            {
                SourceAccountId = accountId,
                Amount = amount,
                RecipientName = "Recipient",
                Description = "Payment",
                DestinationBankSwiftCode = "XYZ",
                DestinationBankName = "Other bank",
                DestinationBankCountry = "Uzbekistan",
                DestinationBankAccountUniqueId = "XYZ00000001"
            };

        private async Task<T> WithHelperAsync<T>(Func<IGlobalTransferHelper, Task<T>> action)
        {
            using var scope = this.factory.Services.CreateScope();

            return await action(scope.ServiceProvider.GetRequiredService<IGlobalTransferHelper>());
        }

        private Task<GlobalTransferResult> SendAsync(decimal amount, HttpStatusCode status)
        {
            this.factory.CentralApi.ResponseStatus = status;

            return this.WithHelperAsync(async helper => await helper.TransferMoneyAsync(
                Transfer((await this.factory.GetSeededAccountAsync()).Id, amount)));
        }

        private Task RetryAsync(HttpStatusCode status)
        {
            this.factory.CentralApi.ResponseStatus = status;

            return this.WithHelperAsync(async helper =>
            {
                await helper.RetryPendingTransfersAsync(TimeSpan.Zero, TimeSpan.Zero);
                return true;
            });
        }

        private Task<int> PendingCountAsync() => this.factory.WithDbAsync(db => db.PendingTransfers.CountAsync());

        private async Task<decimal> BalanceAsync() => (await this.factory.GetSeededAccountAsync()).Balance;

        [Fact]
        public async Task Transfer_FlowsThroughEveryOutcome()
        {
            var start = await this.BalanceAsync();

            // Accepted: money gone, nothing pending
            (await this.SendAsync(10, HttpStatusCode.OK)).Should().Be(GlobalTransferResult.Succeeded);
            (await this.PendingCountAsync()).Should().Be(0);
            (await this.BalanceAsync()).Should().Be(start - 10);

            // Refused: money back at once, nothing pending
            (await this.SendAsync(20, HttpStatusCode.BadRequest)).Should().Be(GlobalTransferResult.GeneralFailure);
            (await this.PendingCountAsync()).Should().Be(0);
            (await this.BalanceAsync()).Should().Be(start - 10);

            // Unknown, then accepted on retry: money stays gone
            (await this.SendAsync(30, HttpStatusCode.GatewayTimeout)).Should().Be(GlobalTransferResult.Pending);
            (await this.PendingCountAsync()).Should().Be(1);
            await this.RetryAsync(HttpStatusCode.InternalServerError);
            (await this.PendingCountAsync()).Should().Be(1);
            await this.RetryAsync(HttpStatusCode.OK);
            (await this.PendingCountAsync()).Should().Be(0);
            (await this.BalanceAsync()).Should().Be(start - 40);

            // Unknown, then refused on retry: refunded once however often it runs
            (await this.SendAsync(40, HttpStatusCode.BadGateway)).Should().Be(GlobalTransferResult.Pending);
            (await this.BalanceAsync()).Should().Be(start - 80);
            await this.RetryAsync(HttpStatusCode.BadRequest);
            await this.RetryAsync(HttpStatusCode.BadRequest);
            (await this.PendingCountAsync()).Should().Be(0);
            (await this.BalanceAsync()).Should().Be(start - 40);

            this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;
        }

        [Fact]
        public async Task AdminPage_ShouldListPendingTransfersForAdministratorsOnly()
        {
            (await this.SendAsync(3, HttpStatusCode.BadGateway)).Should().Be(GlobalTransferResult.Pending);
            var reference = await this.factory.WithDbAsync(db => db.PendingTransfers.Select(p => p.ReferenceNumber).FirstAsync());
            var account = await this.factory.GetSeededAccountAsync();
            var options = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };

            var user = this.factory.CreateClient(options);
            TestAuthHandler.SignIn(user, BankWebFactory.SeededUserName, account.UserId);
            var admin = this.factory.CreateClient(options);
            TestAuthHandler.SignIn(admin, BankWebFactory.SeededUserName, account.UserId, GlobalConstants.AdministratorRoleName);

            (await user.GetAsync(Url)).StatusCode.Should().NotBe(HttpStatusCode.OK);
            var response = await admin.GetAsync(Url);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain(reference);

            await this.RetryAsync(HttpStatusCode.OK);
        }

        private async Task<HttpResponseMessage> PressAsync(string action, string reference)
        {
            var account = await this.factory.GetSeededAccountAsync();
            var admin = this.factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            TestAuthHandler.SignIn(admin, BankWebFactory.SeededUserName, account.UserId, GlobalConstants.AdministratorRoleName);

            var page = await (await admin.GetAsync(Url)).Content.ReadAsStringAsync();
            var token = Regex.Match(page, "__RequestVerificationToken\\\" type=\\\"hidden\\\" value=\\\"([^\\\"]+)").Groups[1].Value;

            return await admin.PostAsync($"{Url}/{action}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["referenceNumber"] = reference,
                ["__RequestVerificationToken"] = token
            }));
        }

        [Fact]
        public async Task AdminRefund_ShouldReturnTheMoneyOnce()
        {
            var start = await this.BalanceAsync();
            (await this.SendAsync(5, HttpStatusCode.BadGateway)).Should().Be(GlobalTransferResult.Pending);
            var reference = await this.factory.WithDbAsync(db => db.PendingTransfers.Select(p => p.ReferenceNumber).FirstAsync());
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;

            (await this.PressAsync("Refund", reference)).StatusCode.Should().Be(HttpStatusCode.Redirect);
            (await this.PressAsync("Refund", reference)).StatusCode.Should().Be(HttpStatusCode.Redirect);

            (await this.PendingCountAsync()).Should().Be(0);
            (await this.BalanceAsync()).Should().Be(start);
        }

        [Fact]
        public async Task AdminRetry_ShouldMakeAStoppedTransferDueAgain()
        {
            (await this.SendAsync(6, HttpStatusCode.BadGateway)).Should().Be(GlobalTransferResult.Pending);
            var reference = await this.factory.WithDbAsync(async db =>
            {
                var pending = await db.PendingTransfers.FirstAsync();
                pending.Attempts = GlobalTransferHelper.MaxDeliveryAttempts;
                await db.SaveChangesAsync();
                return pending.ReferenceNumber;
            });

            await this.RetryAsync(HttpStatusCode.OK);
            (await this.PendingCountAsync()).Should().Be(1);

            (await this.PressAsync("Retry", reference)).StatusCode.Should().Be(HttpStatusCode.Redirect);
            await this.RetryAsync(HttpStatusCode.OK);

            (await this.PendingCountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task Retry_WhenAttemptsRunOut_ShouldLogCriticalAndKeepTheMoneyPending()
        {
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.BadGateway;
            var moneyTransfers = new Mock<IMoneyTransferService>();
            var logger = new Mock<ILogger<GlobalTransferHelper>>();
            moneyTransfers
                .Setup(s => s.GetPendingTransfersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new[]
                {
                    new PendingTransferServiceModel
                    {
                        ReferenceNumber = "1",
                        Payload = JsonConvert.SerializeObject(new CentralApiSubmitTransferDto())
                    }
                });
            moneyTransfers.Setup(s => s.AddPendingTransferAttemptAsync("1")).ReturnsAsync(int.MaxValue);

            await this.CreateHelper(moneyTransfers, logger).RetryPendingTransfersAsync(TimeSpan.Zero, TimeSpan.Zero);

            logger.Verify(
                l => l.Log(
                    LogLevel.Critical,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
            moneyTransfers.Verify(s => s.RefundPendingTransferAsync(It.IsAny<string>()), Times.Never);
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;
        }

        [Fact]
        public async Task Transfer_WhenRefundThrows_ShouldStillAnswerAndLeaveItToTheRetry()
        {
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.BadRequest;
            var moneyTransfers = new Mock<IMoneyTransferService>();
            moneyTransfers
                .Setup(s => s.WithdrawAsync(It.IsAny<MoneyTransferCreateServiceModel>(), It.IsAny<string>()))
                .ReturnsAsync(MoneyTransferResult.Succeeded);
            moneyTransfers
                .Setup(s => s.RefundPendingTransferAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("database is down"));

            var result = await this.CreateHelper(moneyTransfers, new Mock<ILogger<GlobalTransferHelper>>())
                .TransferMoneyAsync(Transfer("source", 10));

            result.Should().Be(GlobalTransferResult.GeneralFailure);
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;
        }

        [Fact]
        public async Task Transfer_WhenOutcomeIsUnknown_ShouldEnqueueItAndNotifyWhenSettled()
        {
            this.factory.CentralApi.ResponseStatus = HttpStatusCode.BadGateway;
            var moneyTransfers = new Mock<IMoneyTransferService>();
            moneyTransfers
                .Setup(s => s.WithdrawAsync(It.IsAny<MoneyTransferCreateServiceModel>(), It.IsAny<string>()))
                .ReturnsAsync(MoneyTransferResult.Succeeded);
            moneyTransfers
                .Setup(s => s.GetPendingTransferAsync("1", It.IsAny<int>()))
                .ReturnsAsync(new PendingTransferServiceModel
                {
                    ReferenceNumber = "1",
                    Payload = JsonConvert.SerializeObject(new CentralApiSubmitTransferDto())
                });
            moneyTransfers.Setup(s => s.AddPendingTransferAttemptAsync("1")).ReturnsAsync(1);
            var helper = this.CreateHelper(moneyTransfers, new Mock<ILogger<GlobalTransferHelper>>());

            (await helper.TransferMoneyAsync(Transfer("source", 10))).Should().Be(GlobalTransferResult.Pending);
            this.queue.Verify(q => q.EnqueueAsync(It.IsAny<string>()), Times.Once);

            (await helper.DeliverPendingTransferAsync("1")).Should().BeTrue();
            this.queue.Verify(q => q.NotifySettledAsync(It.IsAny<string>()), Times.Never);

            this.factory.CentralApi.ResponseStatus = HttpStatusCode.OK;

            (await helper.DeliverPendingTransferAsync("1")).Should().BeFalse();
            moneyTransfers.Verify(s => s.CompletePendingTransferAsync("1"), Times.Once);
            this.queue.Verify(q => q.NotifySettledAsync("1"), Times.Once);
            (await helper.DeliverPendingTransferAsync("gone")).Should().BeFalse();
        }

        private GlobalTransferHelper CreateHelper(
            Mock<IMoneyTransferService> moneyTransfers, Mock<ILogger<GlobalTransferHelper>> logger)
        {
            var accounts = new Mock<IBankAccountService>();
            accounts
                .Setup(s => s.GetByIdAsync<BankAccountConciseServiceModel>("source"))
                .ReturnsAsync(new BankAccountConciseServiceModel { Id = "source", UniqueId = "ABC1", UserFullName = "Sender" });

            return new GlobalTransferHelper(
                accounts.Object,
                moneyTransfers.Object,
                this.factory.Services.GetRequiredService<IOptions<BankConfiguration>>(),
                this.factory.Services.GetRequiredService<IMapper>(),
                logger.Object,
                this.queue.Object);
        }
    }
}
