namespace BankSystem.Web.Infrastructure.Helpers.GlobalTransferHelpers
{
    using System;
    using System.Threading.Tasks;
    using Models;

    public interface IGlobalTransferHelper
    {
        Task<GlobalTransferResult> TransferMoneyAsync(GlobalTransferDto model);

        /// <summary>
        /// Delivers again the transfers without a confirmed outcome that are older than the given age
        /// and that nothing has tried for the given gap, so a transfer the queue is working on is left alone.
        /// A repeated delivery is safe: the receiving bank credits a reference number once.
        /// </summary>
        Task RetryPendingTransfersAsync(TimeSpan minimumAge, TimeSpan attemptGap);

        /// <summary>
        /// Delivers one pending transfer again and says whether it needs another try.
        /// </summary>
        Task<bool> DeliverPendingTransferAsync(string referenceNumber);
    }
}