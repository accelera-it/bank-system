namespace BankSystem.Services.MoneyTransfer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Mapster;
    using MapsterMapper;
    using BankSystem.Models;
    using Common;
    using Data;
    using Microsoft.EntityFrameworkCore;
    using Models.MoneyTransfer;

    public class MoneyTransferService : BaseService, IMoneyTransferService
    {
        // Balance is a concurrency token: a save that lost a race reloads the accounts and tries again
        private const int MaxSaveAttempts = 3;
        private const string RefundDescription = "Refund of a failed transfer";

        private readonly IMapper mapper;

        public MoneyTransferService(BankSystemDbContext context, IMapper mapper)
            : base(context)
        {
            this.mapper = mapper;
        }

        public async Task<IEnumerable<T>> GetMoneyTransferAsync<T>(string referenceNumber)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .AsNoTracking()
                .Where(t => t.ReferenceNumber == referenceNumber)
                .ProjectToType<T>(this.mapper.Config)
                .ToArrayAsync();

        public async Task<int> GetCountOfAllMoneyTransfersForUserAsync(string userId)
            => await this.Context
                .Transfers
                .CountAsync(t => t.Account.UserId == userId);

        public async Task<IEnumerable<T>> GetMoneyTransfersAsync<T>(string userId, int pageIndex = 1, int count = int.MaxValue)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .AsNoTracking()
                .Where(t => t.Account.UserId == userId)
                .OrderByDescending(mt => mt.MadeOn)
                .Skip((pageIndex - 1) * count)
                .Take(count)
                .ProjectToType<T>(this.mapper.Config)
                .ToArrayAsync();

        public async Task<int> GetCountOfAllMoneyTransfersForAccountAsync(string accountId)
            => await this.Context
                .Transfers
                .CountAsync(t => t.AccountId == accountId);

        public async Task<IEnumerable<T>> GetMoneyTransfersForAccountAsync<T>(string accountId, int pageIndex = 1, int count = int.MaxValue)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .AsNoTracking()
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(mt => mt.MadeOn)
                .Skip((pageIndex - 1) * count)
                .Take(count)
                .ProjectToType<T>(this.mapper.Config)
                .ToArrayAsync();

        public async Task<IEnumerable<T>> GetLast10MoneyTransfersForUserAsync<T>(string userId)
            where T : MoneyTransferBaseServiceModel
            => await this.Context
                .Transfers
                .AsNoTracking()
                .Where(mt => mt.Account.UserId == userId)
                .OrderByDescending(mt => mt.MadeOn)
                .Take(10)
                .ProjectToType<T>(this.mapper.Config)
                .ToArrayAsync();

        public async Task<bool> CreateMoneyTransferAsync<T>(T model)
            where T : MoneyTransferBaseServiceModel
        {
            if (!this.IsEntityStateValid(model))
            {
                return false;
            }

            for (var attempt = 0; attempt < MaxSaveAttempts; attempt++)
            {
                // A fresh entity each time: a failed save leaves the old account on the navigation
                var dbModel = this.mapper.Map<MoneyTransfer>(model);
                var userAccount = await this.Context
                    .Accounts
                    .Where(u => u.Id == dbModel.AccountId)
                    .SingleOrDefaultAsync();
                if (userAccount == null)
                {
                    return false;
                }

                userAccount.Balance += dbModel.Amount;
                await this.Context.Transfers.AddAsync(dbModel);

                if (await this.TrySaveAsync())
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<MoneyTransferResult> TransferAsync(
            MoneyTransferCreateServiceModel source, MoneyTransferCreateServiceModel destination)
        {
            if (!this.IsEntityStateValid(source) || !this.IsEntityStateValid(destination)
                || source.AccountId == destination.AccountId
                || source.Amount >= 0 || destination.Amount != -source.Amount)
            {
                return MoneyTransferResult.Failed;
            }

            return await this.SaveAsync(source, destination, null);
        }

        public async Task<MoneyTransferResult> WithdrawAsync(MoneyTransferCreateServiceModel model, string payload)
        {
            // A merchant chooses this reference number, so it cannot be trusted to be new
            if (!this.IsEntityStateValid(model) || model.Amount >= 0
                || await this.Context.Transfers.AnyAsync(t => t.ReferenceNumber == model.ReferenceNumber && t.Amount < 0))
            {
                return MoneyTransferResult.Failed;
            }

            return await this.SaveAsync(model, null, payload);
        }

        public async Task<IEnumerable<PendingTransferServiceModel>> GetPendingTransfersAsync(
            DateTime createdBefore, int maxAttempts, int count)
            => await this.Context
                .PendingTransfers
                .AsNoTracking()
                .Where(p => p.CreatedOn < createdBefore && p.Attempts < maxAttempts)
                .OrderBy(p => p.CreatedOn)
                .Take(count)
                .Select(p => new PendingTransferServiceModel
                {
                    ReferenceNumber = p.ReferenceNumber,
                    Payload = p.Payload
                })
                .ToArrayAsync();

        public async Task CompletePendingTransferAsync(string referenceNumber)
        {
            var pending = await this.Context.PendingTransfers.FindAsync(referenceNumber);
            if (pending != null)
            {
                this.Context.PendingTransfers.Remove(pending);
                await this.TrySaveAsync();
            }
        }

        public async Task<bool> RefundPendingTransferAsync(string referenceNumber)
        {
            for (var attempt = 0; attempt < MaxSaveAttempts; attempt++)
            {
                var pending = await this.Context.PendingTransfers.FindAsync(referenceNumber);
                if (pending == null)
                {
                    return true;
                }

                var withdrawal = await this.Context
                    .Transfers
                    .AsNoTracking()
                    .SingleAsync(t => t.ReferenceNumber == referenceNumber
                        && t.AccountId == pending.AccountId
                        && t.Amount < 0);
                var account = await this.Context.Accounts.SingleAsync(a => a.Id == pending.AccountId);

                account.Balance -= withdrawal.Amount;
                await this.Context.Transfers.AddAsync(new MoneyTransfer
                {
                    Description = RefundDescription,
                    Amount = -withdrawal.Amount,
                    MadeOn = DateTime.UtcNow,
                    AccountId = withdrawal.AccountId,
                    Source = withdrawal.Source,
                    Destination = withdrawal.Destination,
                    SenderName = withdrawal.SenderName,
                    RecipientName = withdrawal.RecipientName,
                    ReferenceNumber = withdrawal.ReferenceNumber
                });
                this.Context.PendingTransfers.Remove(pending);

                // The removed row makes a second refund fail here instead of paying twice
                if (await this.TrySaveAsync())
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<TransferState> GetTransferStateAsync(string referenceNumber)
        {
            if (await this.Context.PendingTransfers.AnyAsync(p => p.ReferenceNumber == referenceNumber))
            {
                return TransferState.Pending;
            }

            var accountId = await this.Context
                .Transfers
                .Where(t => t.ReferenceNumber == referenceNumber && t.Amount < 0)
                .Select(t => t.AccountId)
                .FirstOrDefaultAsync();
            if (accountId == null)
            {
                return TransferState.NotFound;
            }

            var refunded = await this.GetReceivedAmountAsync(accountId, referenceNumber) != null;

            return refunded ? TransferState.Refunded : TransferState.Completed;
        }

        public async Task<IEnumerable<PendingTransferListingServiceModel>> GetAllPendingTransfersAsync()
            => await this.Context
                .PendingTransfers
                .AsNoTracking()
                .OrderBy(p => p.CreatedOn)
                .Select(p => new PendingTransferListingServiceModel
                {
                    ReferenceNumber = p.ReferenceNumber,
                    AccountUniqueId = p.Account.UniqueId,
                    Amount = this.Context.Transfers
                        .Where(t => t.ReferenceNumber == p.ReferenceNumber && t.AccountId == p.AccountId && t.Amount < 0)
                        .Select(t => -t.Amount)
                        .FirstOrDefault(),
                    CreatedOn = p.CreatedOn,
                    Attempts = p.Attempts
                })
                .ToArrayAsync();

        public async Task ResetPendingTransferAttemptsAsync(string referenceNumber)
        {
            var pending = await this.Context.PendingTransfers.FindAsync(referenceNumber);
            if (pending != null)
            {
                pending.Attempts = 0;
                await this.TrySaveAsync();
            }
        }

        public async Task<int> AddPendingTransferAttemptAsync(string referenceNumber)
        {
            var pending = await this.Context.PendingTransfers.FindAsync(referenceNumber);
            if (pending == null)
            {
                return 0;
            }

            pending.Attempts++;
            await this.TrySaveAsync();

            return pending.Attempts;
        }

        public async Task<bool> ReceiveMoneyTransferAsync(MoneyTransferCreateServiceModel model)
        {
            var received = await this.GetReceivedAmountAsync(model.AccountId, model.ReferenceNumber);
            if (received != null)
            {
                return received == model.Amount;
            }

            try
            {
                return await this.CreateMoneyTransferAsync(model);
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                // Two deliveries raced: the unique index let one in
                this.Context.ChangeTracker.Clear();

                received = await this.GetReceivedAmountAsync(model.AccountId, model.ReferenceNumber);
                if (received == null)
                {
                    throw;
                }

                return received == model.Amount;
            }
        }

        public async Task<decimal?> GetReceivedAmountAsync(string accountId, string referenceNumber)
            => await this.Context
                .Transfers
                .AsNoTracking()
                .Where(t => t.AccountId == accountId
                    && t.ReferenceNumber == referenceNumber
                    && t.Amount > 0)
                .Select(t => (decimal?)t.Amount)
                .FirstOrDefaultAsync();

        private async Task<MoneyTransferResult> SaveAsync(
            MoneyTransferCreateServiceModel source, MoneyTransferCreateServiceModel destination, string payload)
        {
            for (var attempt = 0; attempt < MaxSaveAttempts; attempt++)
            {
                var debit = this.mapper.Map<MoneyTransfer>(source);
                var sourceAccount = await this.Context
                    .Accounts
                    .SingleOrDefaultAsync(a => a.Id == debit.AccountId);
                if (sourceAccount == null)
                {
                    return MoneyTransferResult.Failed;
                }

                if (sourceAccount.Balance + debit.Amount < 0)
                {
                    return MoneyTransferResult.InsufficientFunds;
                }

                sourceAccount.Balance += debit.Amount;
                await this.Context.Transfers.AddAsync(debit);

                if (destination != null)
                {
                    var credit = this.mapper.Map<MoneyTransfer>(destination);
                    var destinationAccount = await this.Context
                        .Accounts
                        .SingleOrDefaultAsync(a => a.Id == credit.AccountId);
                    if (destinationAccount == null)
                    {
                        this.Context.ChangeTracker.Clear();

                        return MoneyTransferResult.Failed;
                    }

                    destinationAccount.Balance += credit.Amount;
                    await this.Context.Transfers.AddAsync(credit);
                }

                if (payload != null)
                {
                    await this.Context.PendingTransfers.AddAsync(new PendingTransfer
                    {
                        ReferenceNumber = debit.ReferenceNumber,
                        AccountId = debit.AccountId,
                        Payload = payload,
                        CreatedOn = DateTime.UtcNow
                    });
                }

                // One SaveChanges is one database transaction, so the parts cannot split
                if (await this.TrySaveAsync())
                {
                    return MoneyTransferResult.Succeeded;
                }
            }

            return MoneyTransferResult.Failed;
        }

        private async Task<bool> TrySaveAsync()
        {
            try
            {
                await this.Context.SaveChangesAsync();

                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                this.Context.ChangeTracker.Clear();

                return false;
            }
        }
    }
}