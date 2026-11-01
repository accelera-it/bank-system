namespace BankSystem.Services.MoneyTransfer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Models.MoneyTransfer;

    public interface IMoneyTransferService
    {
        Task<IEnumerable<T>> GetMoneyTransfersAsync<T>(string userId, int pageIndex = 1, int count = int.MaxValue)
            where T : MoneyTransferBaseServiceModel;

        Task<IEnumerable<T>> GetMoneyTransfersForAccountAsync<T>(string accountId, int pageIndex = 1, int count = int.MaxValue)
            where T : MoneyTransferBaseServiceModel;

        Task<IEnumerable<T>> GetLast10MoneyTransfersForUserAsync<T>(string userId)
            where T : MoneyTransferBaseServiceModel;

        Task<bool> CreateMoneyTransferAsync<T>(T model)
            where T : MoneyTransferBaseServiceModel;

        /// <summary>
        /// Moves money between two accounts of this bank: both sides are saved or neither is.
        /// The source model carries a negative amount, the destination model a positive one.
        /// </summary>
        Task<MoneyTransferResult> TransferAsync(
            MoneyTransferCreateServiceModel source, MoneyTransferCreateServiceModel destination);

        /// <summary>
        /// Takes money out of an account unless that would overdraw it and, in the same database transaction,
        /// records what still has to be delivered. The model carries a negative amount.
        /// </summary>
        Task<MoneyTransferResult> WithdrawAsync(MoneyTransferCreateServiceModel model, string payload);

        /// <summary>
        /// The amount already credited to this account under this reference number, if any.
        /// </summary>
        Task<decimal?> GetReceivedAmountAsync(string accountId, string referenceNumber);

        /// <summary>
        /// Where an outgoing transfer stands, for a merchant that has no answer yet.
        /// </summary>
        Task<TransferState> GetTransferStateAsync(string referenceNumber);

        Task<IEnumerable<PendingTransferListingServiceModel>> GetAllPendingTransfersAsync();

        /// <summary>
        /// Gives a transfer that ran out of attempts a new set of retries.
        /// </summary>
        Task ResetPendingTransferAttemptsAsync(string referenceNumber);

        Task<PendingTransferServiceModel> GetPendingTransferAsync(string referenceNumber, int maxAttempts);

        Task<IEnumerable<PendingTransferServiceModel>> GetPendingTransfersAsync(
            DateTime createdBefore, int maxAttempts, int count);

        /// <summary>
        /// Delivery was confirmed: the transfer is no longer pending.
        /// </summary>
        Task CompletePendingTransferAsync(string referenceNumber);

        /// <summary>
        /// Delivery was refused: returns the money and ends the pending transfer in one database transaction.
        /// Safe to call twice. Returns false if the refund could not be saved.
        /// </summary>
        Task<bool> RefundPendingTransferAsync(string referenceNumber);

        /// <summary>
        /// Counts a delivery with an unknown outcome and returns the number of attempts so far.
        /// </summary>
        Task<int> AddPendingTransferAttemptAsync(string referenceNumber);

        /// <summary>
        /// Credits an incoming transfer once: delivering the same reference number again changes nothing
        /// and succeeds, unless the amount differs.
        /// </summary>
        Task<bool> ReceiveMoneyTransferAsync(MoneyTransferCreateServiceModel model);

        Task<IEnumerable<T>> GetMoneyTransferAsync<T>(string referenceNumber)
            where T : MoneyTransferBaseServiceModel;

        Task<int> GetCountOfAllMoneyTransfersForAccountAsync(string accountId);

        Task<int> GetCountOfAllMoneyTransfersForUserAsync(string userId);
    }
}