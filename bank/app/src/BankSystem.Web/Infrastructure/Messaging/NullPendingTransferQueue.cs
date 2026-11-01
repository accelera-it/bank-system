namespace BankSystem.Web.Infrastructure.Messaging
{
    using System.Threading.Tasks;

    /// <summary>
    /// With no broker (tests, plain dotnet run) PendingTransferRetryService does all the retrying.
    /// </summary>
    public class NullPendingTransferQueue : IPendingTransferQueue
    {
        public Task EnqueueAsync(string referenceNumber) => Task.CompletedTask;

        public Task RetryLaterAsync(string referenceNumber) => Task.CompletedTask;

        public Task NotifySettledAsync(string referenceNumber) => Task.CompletedTask;
    }
}
