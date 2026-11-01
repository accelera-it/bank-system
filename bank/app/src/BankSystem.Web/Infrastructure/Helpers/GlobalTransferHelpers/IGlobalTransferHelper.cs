namespace BankSystem.Web.Infrastructure.Helpers.GlobalTransferHelpers
{
    using System;
    using System.Threading.Tasks;
    using Models;

    public interface IGlobalTransferHelper
    {
        Task<GlobalTransferResult> TransferMoneyAsync(GlobalTransferDto model);

        /// <summary>
        /// Delivers again the transfers without a confirmed outcome that are older than the given age.
        /// A repeated delivery is safe: the receiving bank credits a reference number once.
        /// </summary>
        Task RetryPendingTransfersAsync(TimeSpan minimumAge);
    }
}