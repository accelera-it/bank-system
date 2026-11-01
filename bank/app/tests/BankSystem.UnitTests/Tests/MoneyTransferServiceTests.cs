namespace BankSystem.Services.UnitTests.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using BankSystem.Models;
    using Common;
    using Data;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Models.MoneyTransfer;
    using MoneyTransfer;
    using Moq;
    using Xunit;

    public class MoneyTransferServiceTests : BaseTest
    {
        public MoneyTransferServiceTests()
        {
            this.dbContext = this.DatabaseInstance;
            this.moneyTransferService = new MoneyTransferService(
                this.dbContext,
                this.Mapper);
        }

        private const string SampleId = "gsdxcvew-sdfdscx-xcgds";
        private const string SampleDescription = "I'm sending money due to...";
        private const decimal SampleAmount = 10;
        private const string SampleRecipientName = "test";
        private const string SampleSenderName = "melik";
        private const string SampleDestination = "dgsfcx-arq12wasdasdzxxcv";

        private const string SampleBankAccountName = "Test bank account name";
        private const string SampleBankAccountId = "1";
        private const string SampleDestinationAccountId = "2";
        private const string SampleBankAccountUniqueId = "UniqueId";
        private const string SampleReferenceNumber = "18832258557125540";

        private const string SampleUserFullname = "user user";
        private const string SampleUserId = "adfsdvxc-123ewsf";

        private readonly BankSystemDbContext dbContext;
        private readonly IMoneyTransferService moneyTransferService;

        [Theory]
        [InlineData(" 031069130864508423")]
        [InlineData("24675875i6452436 ")]
        [InlineData("! 68o4473485669 ")]
        [InlineData("   845768798069  10   ")]
        [InlineData("45848o02835yu56=")]
        public async Task GetMoneyTransferAsync_WithInvalidNumber_Should_ReturnEmptyCollection(string referenceNumber)
        {
            // Arrange
            await this.SeedMoneyTransfersAsync();
            // Act
            var result =
                await this.moneyTransferService
                    .GetMoneyTransferAsync<MoneyTransferListingServiceModel>(referenceNumber);

            // Assert
            result
                .Should()
                .BeNullOrEmpty();
        }

        [Fact]
        public async Task GetMoneyTransferAsync_WithValidNumber_Should_ReturnCorrectEntities()
        {
            // Arrange
            const string expectedRefNumber = SampleReferenceNumber;
            await this.SeedBankAccountAsync();
            await this.dbContext.Transfers.AddAsync(new MoneyTransfer
            {
                Description = SampleDescription,
                Amount = SampleAmount,
                Account = await this.dbContext.Accounts.FirstOrDefaultAsync(),
                Destination = SampleDestination,
                Source = SampleBankAccountId,
                SenderName = SampleSenderName,
                RecipientName = SampleRecipientName,
                MadeOn = DateTime.UtcNow,
                ReferenceNumber = expectedRefNumber,
            });
            await this.dbContext.SaveChangesAsync();

            // Act
            var result =
                await this.moneyTransferService.GetMoneyTransferAsync<MoneyTransferListingServiceModel>(
                    expectedRefNumber);

            // Assert
            result
                .Should()
                .NotBeNullOrEmpty()
                .And
                .Match(t => t.All(x => x.ReferenceNumber == expectedRefNumber));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" dassdgdf")]
        [InlineData(" ")]
        [InlineData("!  ")]
        [InlineData("     10   ")]
        [InlineData("  sdgsfcx-arq12wasdasdzxxcvc   ")]
        public async Task GetAllMoneyTransfersAsync_WithInvalidUserId_Should_ReturnEmptyCollection(string userId)
        {
            // Arrange
            await this.SeedMoneyTransfersAsync();
            // Act
            var result =
                await this.moneyTransferService.GetMoneyTransfersAsync<MoneyTransferListingServiceModel>(userId);

            // Assert
            result
                .Should()
                .BeNullOrEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" dassdgdf")]
        [InlineData(" ")]
        [InlineData("!  ")]
        [InlineData("     10   ")]
        [InlineData("  sdgsfcx-arq12wasdasdzxxcvc   ")]
        public async Task GetAllMoneyTransfersForAccountAsync_WithInvalidUserId_Should_ReturnEmptyCollection(
            string accountId)
        {
            // Arrange
            await this.SeedMoneyTransfersAsync();
            // Act
            var result =
                await this.moneyTransferService
                    .GetMoneyTransfersForAccountAsync<MoneyTransferListingServiceModel>(accountId);

            // Assert
            result
                .Should()
                .BeNullOrEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" dassdgdf")]
        [InlineData(" ")]
        [InlineData("!  ")]
        [InlineData("     10   ")]
        [InlineData("  sdgsfcx-arq12wasdasdzxxcvc   ")]
        public async Task GetLast10MoneyTransfersForUserAsync_WithInvalidUserId_Should_ReturnEmptyCollection(
            string userId)
        {
            // Arrange
            await this.SeedMoneyTransfersAsync();
            // Act
            var result =
                await this.moneyTransferService
                    .GetLast10MoneyTransfersForUserAsync<MoneyTransferListingServiceModel>(userId);

            // Assert
            result
                .Should()
                .BeNullOrEmpty();
        }

        [Fact]
        public async Task GetCountOfAllMoneyTransfersForUserAsync_Should_ReturnCorrectCount()
        {
            // Arrange
            await this.SeedMoneyTransfersAsync();

            // Act
            var result =
                await this.moneyTransferService.GetCountOfAllMoneyTransfersForUserAsync(SampleUserId);

            // Assert
            result
                .Should()
                .Be(await this.dbContext.Transfers.CountAsync(t => t.Account.UserId == SampleUserId));
        }

        [Fact]
        public async Task GetCountOfAllMoneyTransfersForAccountAsync_Should_ReturnCorrectCount()
        {
            // Arrange
            await this.SeedMoneyTransfersAsync();

            // Act
            var result =
                await this.moneyTransferService.GetCountOfAllMoneyTransfersForAccountAsync(SampleBankAccountId);

            // Assert
            result
                .Should()
                .Be(await this.dbContext.Transfers.CountAsync(t => t.AccountId == SampleBankAccountId));
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithDifferentId_Should_ReturnFalse()
        {
            // Arrange
            await this.SeedBankAccountAsync();
            var model = PrepareCreateModel(accountId: "another id");

            // Act
            var result = await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            result
                .Should()
                .BeFalse();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithDifferentId_Should_Not_AddMoneyTransferInDatabase()
        {
            // Arrange
            await this.SeedBankAccountAsync();
            var model = PrepareCreateModel(accountId: "another id");

            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithEmptyModel_Should_ReturnFalse()
        {
            // Act
            var result =
                await this.moneyTransferService.CreateMoneyTransferAsync(new MoneyTransferCreateServiceModel());

            // Assert
            result
                .Should()
                .BeFalse();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithEmptyModel_Should_Not_InsertInDatabase()
        {
            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(new MoneyTransferCreateServiceModel());

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithInvalidDescription_Should_ReturnFalse()
        {
            // Arrange
            var invalidDescription = new string('m', ModelConstants.MoneyTransfer.DescriptionMaxLength + 1);
            var model = PrepareCreateModel(invalidDescription);

            // Act
            var result = await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            result
                .Should()
                .BeFalse();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithInvalidDescription_Should_Not_InsertInDatabase()
        {
            // Arrange
            var invalidDescription = new string('m', ModelConstants.MoneyTransfer.DescriptionMaxLength + 1);
            var model = PrepareCreateModel(invalidDescription);

            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithInvalidDestinationBankAccountUniqueId_Should_ReturnFalse()
        {
            // Arrange
            var invalidDestinationBankAccountUniqueId =
                new string('m', ModelConstants.BankAccount.UniqueIdMaxLength + 1);
            var model = PrepareCreateModel(destinationBankUniqueId: invalidDestinationBankAccountUniqueId);

            // Act
            var result = await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            result
                .Should()
                .BeFalse();
        }

        [Fact]
        public async Task
            CreateMoneyTransferAsync_WithInvalidDestinationBankAccountUniqueId_Should_Not_InsertInDatabase()
        {
            // Arrange
            var invalidDestinationBankAccountUniqueId =
                new string('m', ModelConstants.BankAccount.UniqueIdMaxLength + 1);
            var model = PrepareCreateModel(destinationBankUniqueId: invalidDestinationBankAccountUniqueId);

            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithInvalidRecipientName_Should_ReturnFalse()
        {
            // Arrange
            var invalidRecipientName = new string('m', ModelConstants.User.FullNameMaxLength + 1);
            var model = PrepareCreateModel(recipientName: invalidRecipientName);

            // Act
            var result = await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            result
                .Should()
                .BeFalse();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithInvalidRecipientName_Should_NotInsertInDatabase()
        {
            // Arrange
            var invalidRecipientName = new string('m', ModelConstants.User.FullNameMaxLength + 1);
            var model = PrepareCreateModel(recipientName: invalidRecipientName);

            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithInvalidSenderName_Should_ReturnFalse()
        {
            // Arrange
            var invalidSenderName = new string('m', ModelConstants.User.FullNameMaxLength + 1);
            var model = PrepareCreateModel(senderName: invalidSenderName);

            // Act
            var result = await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            result
                .Should()
                .BeFalse();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithInvalidSenderName_Should_Not_InsertInDatabase()
        {
            // Arrange
            var invalidSenderName = new string('m', ModelConstants.User.FullNameMaxLength + 1);
            var model = PrepareCreateModel(senderName: invalidSenderName);

            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithInvalidSource_Should_ReturnFalse()
        {
            // Arrange
            var invalidSource = new string('m', ModelConstants.BankAccount.UniqueIdMaxLength + 1);
            var model = PrepareCreateModel(source: invalidSource);

            // Act
            var result = await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            result
                .Should()
                .BeFalse();
        }

        [Fact]
        public async Task
            CreateMoneyTransferAsync_WithInvalidSource_Should_Not_InsertInDatabase()
        {
            // Arrange
            var invalidSource = new string('m', ModelConstants.BankAccount.UniqueIdMaxLength + 1);
            var model = PrepareCreateModel(source: invalidSource);

            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .BeEmpty();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithValidModel_AndNegativeAmount_Should_ReturnTrue()
        {
            // Arrange
            var dbCount = this.dbContext.Transfers.Count();
            await this.SeedBankAccountAsync();
            // Setting amount to negative means we're sending money.
            var model = PrepareCreateModel(amount: -10);

            // Act
            var result = await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            result
                .Should()
                .BeTrue();
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithValidModel_AndNegativeAmount_Should_InsertInDatabase()
        {
            // Arrange
            var dbCount = this.dbContext.Transfers.Count();
            await this.SeedBankAccountAsync();
            // Setting amount to negative means we're sending money.
            var model = PrepareCreateModel(amount: -10);

            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .HaveCount(dbCount + 1);
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithValidModel_Should_ReturnTrue()
        {
            // Arrange
            var dbCount = this.dbContext.Transfers.Count();
            await this.SeedBankAccountAsync();
            var model = PrepareCreateModel();

            // Act
            var result = await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            result
                .Should()
                .BeTrue();
        }

        [Fact]
        public async Task TransferAsync_WithValidModels_Should_MoveMoneyAndInsertBothTransfers()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);

            // Act
            var result = await this.moneyTransferService.TransferAsync(
                PrepareCreateModel(amount: -30), PrepareCreateModel(amount: 30, accountId: SampleDestinationAccountId));

            // Assert
            result.Should().Be(MoneyTransferResult.Succeeded);
            await this.AssertBalancesAsync(this.dbContext, 70, 35);
            this.dbContext.Transfers.Should().HaveCount(2);
        }

        [Fact]
        public async Task TransferAsync_WithInsufficientFunds_Should_ChangeNothing()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 20, 5);

            // Act
            var result = await this.moneyTransferService.TransferAsync(
                PrepareCreateModel(amount: -30), PrepareCreateModel(amount: 30, accountId: SampleDestinationAccountId));

            // Assert
            result.Should().Be(MoneyTransferResult.InsufficientFunds);
            await this.AssertBalancesAsync(this.dbContext, 20, 5);
            this.dbContext.Transfers.Should().BeEmpty();
        }

        [Fact]
        public async Task TransferAsync_WithMissingDestinationAccount_Should_ChangeNothing()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);

            // Act
            var result = await this.moneyTransferService.TransferAsync(
                PrepareCreateModel(amount: -30), PrepareCreateModel(amount: 30, accountId: "missing"));

            // Assert
            result.Should().Be(MoneyTransferResult.Failed);
            await this.AssertBalancesAsync(this.dbContext, 100, 5);
            this.dbContext.Transfers.Should().BeEmpty();
        }

        [Fact]
        public async Task TransferAsync_WithInvalidDestinationModel_Should_ChangeNothing()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);

            // Act
            var result = await this.moneyTransferService.TransferAsync(
                PrepareCreateModel(amount: -30),
                PrepareCreateModel(amount: 30, accountId: SampleDestinationAccountId, recipientName: null));

            // Assert
            result.Should().Be(MoneyTransferResult.Failed);
            await this.AssertBalancesAsync(this.dbContext, 100, 5);
            this.dbContext.Transfers.Should().BeEmpty();
        }

        [Fact]
        public async Task TransferAsync_WhenBalanceChangesDuringSave_Should_RetryOnFreshBalance()
        {
            // Arrange: another request takes 80 of the 100 just before the first save.
            // Transfers are not asserted: InMemory has no transactions, so a refused save keeps its rows.
            var (context, rival) = this.CreateRacingContexts(context => context.Accounts.Find(SampleBankAccountId).Balance -= 80);
            await this.SeedTransferAccountsAsync(rival, 100, 5);
            var service = new MoneyTransferService(context, this.Mapper);

            // Act
            var result = await service.TransferAsync(
                PrepareCreateModel(amount: -10), PrepareCreateModel(amount: 10, accountId: SampleDestinationAccountId));

            // Assert
            result.Should().Be(MoneyTransferResult.Succeeded);
            await this.AssertBalancesAsync(rival, 10, 15);
        }

        [Fact]
        public async Task TransferAsync_WhenRaceLeavesTooLittleMoney_Should_ReturnInsufficientFunds()
        {
            // Arrange: both requests want 80 of the 100, and the rival saves first
            var (context, rival) = this.CreateRacingContexts(context => context.Accounts.Find(SampleBankAccountId).Balance -= 80);
            await this.SeedTransferAccountsAsync(rival, 100, 5);
            var service = new MoneyTransferService(context, this.Mapper);

            // Act
            var result = await service.TransferAsync(
                PrepareCreateModel(amount: -80), PrepareCreateModel(amount: 80, accountId: SampleDestinationAccountId));

            // Assert
            result.Should().Be(MoneyTransferResult.InsufficientFunds);
            await this.AssertBalancesAsync(rival, 20, 5);
        }

        [Fact]
        public async Task WithdrawAsync_WithEnoughMoney_Should_DebitAccount()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);

            // Act
            var result = await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");

            // Assert
            result.Should().Be(MoneyTransferResult.Succeeded);
            await this.AssertBalancesAsync(this.dbContext, 70, 5);
            this.dbContext.Transfers.Should().HaveCount(1);
            this.dbContext.PendingTransfers.Should().ContainSingle(p => p.Payload == "payload");
        }

        [Fact]
        public async Task WithdrawAsync_WithInsufficientFunds_Should_ChangeNothing()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 20, 5);

            // Act
            var result = await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");

            // Assert
            result.Should().Be(MoneyTransferResult.InsufficientFunds);
            await this.AssertBalancesAsync(this.dbContext, 20, 5);
            this.dbContext.Transfers.Should().BeEmpty();
            this.dbContext.PendingTransfers.Should().BeEmpty();
        }

        [Fact]
        public async Task RefundPendingTransferAsync_CalledTwice_Should_RefundOnce()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);
            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");

            // Act
            var first = await this.moneyTransferService.RefundPendingTransferAsync(SampleReferenceNumber);
            var second = await this.moneyTransferService.RefundPendingTransferAsync(SampleReferenceNumber);

            // Assert
            first.Should().BeTrue();
            second.Should().BeTrue();
            await this.AssertBalancesAsync(this.dbContext, 100, 5);
            this.dbContext.PendingTransfers.Should().BeEmpty();
            this.dbContext.Transfers.Should().HaveCount(2);
        }

        [Fact]
        public async Task CompletePendingTransferAsync_Should_KeepWithdrawalAndEndPending()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);
            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");

            // Act
            await this.moneyTransferService.CompletePendingTransferAsync(SampleReferenceNumber);

            // Assert
            await this.AssertBalancesAsync(this.dbContext, 70, 5);
            this.dbContext.PendingTransfers.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTransferStateAsync_Should_FollowTheTransfer()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);

            // Act and assert
            (await this.moneyTransferService.GetTransferStateAsync(SampleReferenceNumber))
                .Should().Be(TransferState.NotFound);

            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");
            (await this.moneyTransferService.GetTransferStateAsync(SampleReferenceNumber))
                .Should().Be(TransferState.Pending);

            await this.moneyTransferService.CompletePendingTransferAsync(SampleReferenceNumber);
            (await this.moneyTransferService.GetTransferStateAsync(SampleReferenceNumber))
                .Should().Be(TransferState.Completed);
        }

        [Fact]
        public async Task GetTransferStateAsync_AfterRefund_Should_ReturnRefunded()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);
            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");

            // Act
            await this.moneyTransferService.RefundPendingTransferAsync(SampleReferenceNumber);

            // Assert
            (await this.moneyTransferService.GetTransferStateAsync(SampleReferenceNumber))
                .Should().Be(TransferState.Refunded);
        }

        [Fact]
        public async Task WithdrawAsync_WithUsedReferenceNumber_Should_ChangeNothing()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);
            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");
            await this.moneyTransferService.CompletePendingTransferAsync(SampleReferenceNumber);

            // Act
            var result = await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");

            // Assert
            result.Should().Be(MoneyTransferResult.Failed);
            await this.AssertBalancesAsync(this.dbContext, 70, 5);
        }

        [Fact]
        public async Task GetAllPendingTransfersAsync_Should_ShowAccountAndAmount()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);
            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");

            // Act
            var result = (await this.moneyTransferService.GetAllPendingTransfersAsync()).Single();

            // Assert
            result.Amount.Should().Be(30);
            result.AccountUniqueId.Should().Be(SampleBankAccountUniqueId);
        }

        [Fact]
        public async Task ResetPendingTransferAttemptsAsync_Should_MakeTransferDueAgain()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);
            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");
            await this.moneyTransferService.AddPendingTransferAttemptAsync(SampleReferenceNumber);

            // Act
            await this.moneyTransferService.ResetPendingTransferAttemptsAsync(SampleReferenceNumber);

            // Assert
            (await this.moneyTransferService.GetPendingTransfersAsync(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(1), 1, 10))
                .Should().ContainSingle();
        }

        [Fact]
        public async Task GetPendingTransfersAsync_Should_SkipTransfersTriedWithinTheGap()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);
            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");
            await this.moneyTransferService.AddPendingTransferAttemptAsync(SampleReferenceNumber);

            // Act
            var future = DateTime.UtcNow.AddHours(1);
            var justTried = await this.moneyTransferService.GetPendingTransfersAsync(
                future, DateTime.UtcNow.AddMinutes(-2), 5, 10);
            var quietForLong = await this.moneyTransferService.GetPendingTransfersAsync(future, future, 5, 10);

            // Assert
            justTried.Should().BeEmpty();
            quietForLong.Should().ContainSingle();
        }

        [Fact]
        public async Task GetPendingTransfersAsync_Should_SkipNewAndExhaustedTransfers()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);
            await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: -30), "payload");
            await this.moneyTransferService.AddPendingTransferAttemptAsync(SampleReferenceNumber);

            // Act
            var tooNew = await this.moneyTransferService.GetPendingTransfersAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), 5, 10);
            var exhausted = await this.moneyTransferService.GetPendingTransfersAsync(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(1), 1, 10);
            var due = await this.moneyTransferService.GetPendingTransfersAsync(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(1), 5, 10);

            // Assert
            tooNew.Should().BeEmpty();
            exhausted.Should().BeEmpty();
            due.Should().ContainSingle(p => p.ReferenceNumber == SampleReferenceNumber);
        }

        [Fact]
        public async Task WithdrawAsync_WithPositiveAmount_Should_ReturnFailed()
        {
            // Arrange
            await this.SeedTransferAccountsAsync(this.dbContext, 100, 5);

            // Act
            var result = await this.moneyTransferService.WithdrawAsync(PrepareCreateModel(amount: 30), "payload");

            // Assert
            result.Should().Be(MoneyTransferResult.Failed);
            await this.AssertBalancesAsync(this.dbContext, 100, 5);
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WhenBalanceChangesDuringSave_Should_KeepBothChanges()
        {
            // Arrange
            var (context, rival) = this.CreateRacingContexts(context => context.Accounts.Find(SampleBankAccountId).Balance += 50);
            await this.SeedTransferAccountsAsync(rival, 100, 5);
            var service = new MoneyTransferService(context, this.Mapper);

            // Act
            var result = await service.CreateMoneyTransferAsync(PrepareCreateModel(amount: 10));

            // Assert
            result.Should().BeTrue();
            await this.AssertBalancesAsync(rival, 160, 5);
        }

        [Fact]
        public async Task CreateMoneyTransferAsync_WithValidModel_Should_InsertInDatabase()
        {
            // Arrange
            var dbCount = this.dbContext.Transfers.Count();
            await this.SeedBankAccountAsync();
            var model = PrepareCreateModel();

            // Act
            await this.moneyTransferService.CreateMoneyTransferAsync(model);

            // Assert
            this.dbContext
                .Transfers
                .Should()
                .HaveCount(dbCount + 1);
        }

        [Fact]
        public async Task GetAllMoneyTransfersAsync_Should_ReturnCorrectCount()
        {
            // Arrange
            const int count = 10;
            await this.SeedBankAccountAsync();
            for (int i = 1; i <= count; i++)
            {
                await this.dbContext.Transfers.AddAsync(new MoneyTransfer
                {
                    Account = await this.dbContext.Accounts.FirstOrDefaultAsync(),
                    Source = "source-account",
                    Destination = "destination-account",
                    SenderName = "Sender",
                    RecipientName = "Recipient",
                    ReferenceNumber = Guid.NewGuid().ToString("N"),
                });
            }

            await this.dbContext.SaveChangesAsync();
            // Act
            var result =
                await this.moneyTransferService.GetMoneyTransfersAsync<MoneyTransferListingServiceModel>(SampleUserId);

            // Assert
            result
                .Should()
                .HaveCount(count);
        }

        [Fact]
        public async Task GetAllMoneyTransfersAsync_Should_ReturnOrderedByMadeOnCollection()
        {
            // Arrange
            await this.SeedBankAccountAsync();
            for (int i = 0; i < 10; i++)
            {
                await this.dbContext.Transfers.AddAsync(new MoneyTransfer
                {
                    Id = $"{SampleId}_{i}",
                    Account = await this.dbContext.Accounts.FirstOrDefaultAsync(),
                    MadeOn = DateTime.UtcNow.AddMinutes(i),
                    Source = "source-account",
                    Destination = "destination-account",
                    SenderName = "Sender",
                    RecipientName = "Recipient",
                    ReferenceNumber = Guid.NewGuid().ToString("N"),
                });
            }

            await this.dbContext.SaveChangesAsync();
            // Act
            var result =
                await this.moneyTransferService.GetMoneyTransfersAsync<MoneyTransferListingServiceModel>(SampleUserId);

            // Assert
            result
                .Should()
                .BeInDescendingOrder(x => x.MadeOn);
        }

        [Fact]
        public async Task GetAllMoneyTransfersAsync_WithValidUserId_Should_ReturnCollectionOfCorrectEntities()
        {
            // Arrange
            var model = await this.SeedMoneyTransfersAsync();
            // Act
            var result =
                await this.moneyTransferService.GetMoneyTransfersAsync<MoneyTransferListingServiceModel>(model.Account
                    .UserId);

            // Assert
            result
                .Should()
                .AllBeAssignableTo<MoneyTransferListingServiceModel>()
                .And
                .Match<IEnumerable<MoneyTransferListingServiceModel>>(x => x.All(c => c.Source == model.Source));
        }

        [Fact]
        public async Task GetAllMoneyTransfersForAccountAsync_Should_ReturnCorrectCount()
        {
            // Arrange
            const int count = 10;
            await this.SeedBankAccountAsync();
            for (int i = 1; i <= count; i++)
            {
                await this.dbContext.Transfers.AddAsync(new MoneyTransfer
                {
                    Account = await this.dbContext.Accounts.FirstOrDefaultAsync(),
                    Source = "source-account",
                    Destination = "destination-account",
                    SenderName = "Sender",
                    RecipientName = "Recipient",
                    ReferenceNumber = Guid.NewGuid().ToString("N"),
                });
            }

            await this.dbContext.SaveChangesAsync();
            // Act
            var result =
                await this.moneyTransferService.GetMoneyTransfersForAccountAsync<MoneyTransferListingServiceModel>(
                    SampleBankAccountId);

            // Assert
            result
                .Should()
                .HaveCount(count);
        }

        [Fact]
        public async Task GetAllMoneyTransfersForAccountAsync_Should_ReturnOrderedByMadeOnCollection()
        {
            // Arrange
            await this.SeedBankAccountAsync();
            for (int i = 0; i < 10; i++)
            {
                await this.dbContext.Transfers.AddAsync(new MoneyTransfer
                {
                    Id = $"{SampleId}_{i}",
                    Account = await this.dbContext.Accounts.FirstOrDefaultAsync(),
                    MadeOn = DateTime.UtcNow.AddMinutes(i),
                    Source = "source-account",
                    Destination = "destination-account",
                    SenderName = "Sender",
                    RecipientName = "Recipient",
                    ReferenceNumber = Guid.NewGuid().ToString("N"),
                });
            }

            await this.dbContext.SaveChangesAsync();
            // Act
            var result =
                await this.moneyTransferService.GetMoneyTransfersForAccountAsync<MoneyTransferListingServiceModel>(
                    SampleBankAccountId);

            // Assert
            result
                .Should()
                .BeInDescendingOrder(x => x.MadeOn);
        }

        [Fact]
        public async Task GetAllMoneyTransfersForAccountAsync_WithValidUserId_Should_ReturnCollectionOfCorrectEntities()
        {
            // Arrange
            var model = await this.SeedMoneyTransfersAsync();
            // Act
            var result =
                await this.moneyTransferService.GetMoneyTransfersForAccountAsync<MoneyTransferListingServiceModel>(
                    model.Account.Id);

            // Assert
            result
                .Should()
                .AllBeAssignableTo<MoneyTransferListingServiceModel>()
                .And
                .Match<IEnumerable<MoneyTransferListingServiceModel>>(x => x.All(c => c.Source == model.Source));
        }

        [Fact]
        public async Task GetLast10MoneyTransfersForUserAsync_Should_ReturnCorrectCount()
        {
            // Arrange
            const int count = 22;
            const int expectedCount = 10;
            await this.SeedBankAccountAsync();
            for (int i = 1; i <= count; i++)
            {
                await this.dbContext.Transfers.AddAsync(new MoneyTransfer
                {
                    Account = await this.dbContext.Accounts.FirstOrDefaultAsync(),
                    Source = "source-account",
                    Destination = "destination-account",
                    SenderName = "Sender",
                    RecipientName = "Recipient",
                    ReferenceNumber = Guid.NewGuid().ToString("N"),
                });
            }

            await this.dbContext.SaveChangesAsync();
            // Act
            var result =
                await this.moneyTransferService.GetLast10MoneyTransfersForUserAsync<MoneyTransferListingServiceModel>(
                    SampleUserId);

            // Assert
            result
                .Should()
                .HaveCount(expectedCount);
        }

        [Fact]
        public async Task GetLast10MoneyTransfersForUserAsync_Should_ReturnOrderedByMadeOnCollection()
        {
            // Arrange
            await this.SeedBankAccountAsync();
            for (int i = 0; i < 22; i++)
            {
                await this.dbContext.Transfers.AddAsync(new MoneyTransfer
                {
                    Id = $"{SampleId}_{i}",
                    Account = await this.dbContext.Accounts.FirstOrDefaultAsync(),
                    MadeOn = DateTime.UtcNow.AddMinutes(i),
                    Source = "source-account",
                    Destination = "destination-account",
                    SenderName = "Sender",
                    RecipientName = "Recipient",
                    ReferenceNumber = Guid.NewGuid().ToString("N"),
                });
            }

            await this.dbContext.SaveChangesAsync();
            // Act
            var result =
                await this.moneyTransferService.GetLast10MoneyTransfersForUserAsync<MoneyTransferListingServiceModel>(
                    SampleUserId);

            // Assert
            result
                .Should()
                .BeInDescendingOrder(x => x.MadeOn);
        }

        [Fact]
        public async Task GetLast10MoneyTransfersForUserAsync_WithValidUserId_Should_ReturnCollectionOfCorrectEntities()
        {
            // Arrange
            var model = await this.SeedMoneyTransfersAsync();
            // Act
            var result =
                await this.moneyTransferService.GetLast10MoneyTransfersForUserAsync<MoneyTransferListingServiceModel>(
                    model.Account.UserId);

            // Assert
            result
                .Should()
                .AllBeAssignableTo<MoneyTransferListingServiceModel>()
                .And
                .Match<IEnumerable<MoneyTransferListingServiceModel>>(x => x.All(c => c.Source == model.Source));
        }

        private static MoneyTransferCreateServiceModel PrepareCreateModel(
            string description = SampleDescription,
            decimal amount = SampleAmount,
            string accountId = SampleBankAccountId,
            string destinationBankUniqueId = SampleDestination,
            string source = SampleBankAccountId,
            string senderName = SampleSenderName,
            string recipientName = SampleRecipientName,
            string referenceNumber = SampleReferenceNumber)
        {
            var model = new MoneyTransferCreateServiceModel
            {
                Description = description,
                Amount = amount,
                AccountId = accountId,
                DestinationBankAccountUniqueId = destinationBankUniqueId,
                Source = source,
                SenderName = senderName,
                RecipientName = recipientName,
                ReferenceNumber = SampleReferenceNumber,
            };

            return model;
        }

        private (BankSystemDbContext Context, BankSystemDbContext Rival) CreateRacingContexts(
            Action<BankSystemDbContext> rivalChange)
        {
            var databaseName = Guid.NewGuid().ToString();
            var rival = new BankSystemDbContext(new DbContextOptionsBuilder<BankSystemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);
            var context = new BankSystemDbContext(new DbContextOptionsBuilder<BankSystemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .AddInterceptors(new RivalSaveInterceptor(databaseName, rivalChange))
                .Options);

            return (context, rival);
        }

        private async Task SeedTransferAccountsAsync(BankSystemDbContext context, decimal source, decimal destination)
        {
            await context.Accounts.AddRangeAsync(
                new BankAccount
                {
                    Id = SampleBankAccountId,
                    Name = SampleBankAccountName,
                    UniqueId = SampleBankAccountUniqueId,
                    UserId = SampleUserId,
                    Balance = source
                },
                new BankAccount
                {
                    Id = SampleDestinationAccountId,
                    Name = SampleBankAccountName,
                    UniqueId = SampleDestination,
                    UserId = SampleUserId,
                    Balance = destination
                });
            await context.SaveChangesAsync();
        }

        private async Task AssertBalancesAsync(BankSystemDbContext context, decimal source, decimal destination)
        {
            context.ChangeTracker.Clear();
            (await context.Accounts.FindAsync(SampleBankAccountId)).Balance.Should().Be(source);
            (await context.Accounts.FindAsync(SampleDestinationAccountId)).Balance.Should().Be(destination);
        }

        private sealed class RivalSaveInterceptor : SaveChangesInterceptor
        {
            private readonly string databaseName;
            private readonly Action<BankSystemDbContext> rivalChange;
            private bool done;

            public RivalSaveInterceptor(string databaseName, Action<BankSystemDbContext> rivalChange)
            {
                this.databaseName = databaseName;
                this.rivalChange = rivalChange;
            }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (!this.done)
                {
                    this.done = true;

                    using var rival = new BankSystemDbContext(new DbContextOptionsBuilder<BankSystemDbContext>()
                        .UseInMemoryDatabase(this.databaseName)
                        .Options);
                    this.rivalChange(rival);
                    rival.SaveChanges();
                }

                return ValueTask.FromResult(result);
            }
        }

        private async Task<MoneyTransfer> SeedMoneyTransfersAsync()
        {
            await this.SeedBankAccountAsync();
            var model = new MoneyTransfer
            {
                Id = SampleId,
                Description = SampleDescription,
                Amount = SampleAmount,
                Account = await this.dbContext.Accounts.FirstOrDefaultAsync(),
                Destination = SampleDestination,
                Source = SampleBankAccountId,
                SenderName = SampleSenderName,
                RecipientName = SampleRecipientName,
                MadeOn = DateTime.UtcNow,
                ReferenceNumber = Guid.NewGuid().ToString("N"),
            };

            await this.dbContext.Transfers.AddAsync(model);
            await this.dbContext.SaveChangesAsync();

            return model;
        }

        private async Task<BankAccount> SeedBankAccountAsync()
        {
            await this.SeedUserAsync();
            var model = new BankAccount
            {
                Id = SampleBankAccountId,
                Name = SampleBankAccountName,
                UniqueId = SampleBankAccountUniqueId,
                User = await this.dbContext.Users.FirstOrDefaultAsync()
            };
            await this.dbContext.Accounts.AddAsync(model);
            await this.dbContext.SaveChangesAsync();

            return model;
        }

        private async Task SeedUserAsync()
        {
            await this.dbContext.Users.AddAsync(new BankUser { Id = SampleUserId, FullName = SampleUserFullname });
            await this.dbContext.SaveChangesAsync();
        }
    }
}