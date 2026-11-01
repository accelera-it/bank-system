namespace BankSystem.Web.Infrastructure.Messaging
{
    using System.Threading.Tasks;

    /// <summary>
    /// Tells the worker and the merchant about pending transfers. PendingTransfers stays the record:
    /// a message that gets lost only delays the transfer until PendingTransferRetryService finds it.
    /// </summary>
    public interface IPendingTransferQueue
    {
        Task EnqueueAsync(string referenceNumber);

        Task RetryLaterAsync(string referenceNumber);

        Task NotifySettledAsync(string referenceNumber);
    }
}
