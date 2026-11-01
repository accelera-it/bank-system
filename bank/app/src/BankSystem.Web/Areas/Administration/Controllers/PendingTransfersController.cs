namespace BankSystem.Web.Areas.Administration.Controllers
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Services.MoneyTransfer;

    public class PendingTransfersController : BaseAdministrationController
    {
        private readonly IMoneyTransferService moneyTransferService;

        public PendingTransfersController(IMoneyTransferService moneyTransferService)
            => this.moneyTransferService = moneyTransferService;

        public async Task<IActionResult> Index()
            => this.View(await this.moneyTransferService.GetAllPendingTransfersAsync());

        [HttpPost]
        public async Task<IActionResult> Retry(string referenceNumber)
        {
            await this.moneyTransferService.ResetPendingTransferAttemptsAsync(referenceNumber);
            this.ShowSuccessMessage("The transfer will be delivered again.");

            return this.RedirectToAction(nameof(this.Index));
        }

        // Only for someone who has checked with the other bank that the money did not arrive
        [HttpPost]
        public async Task<IActionResult> Refund(string referenceNumber)
        {
            if (await this.moneyTransferService.RefundPendingTransferAsync(referenceNumber))
            {
                this.ShowSuccessMessage("The money was returned to the account.");
            }
            else
            {
                this.ShowErrorMessage("The refund could not be saved. Please try again.");
            }

            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
