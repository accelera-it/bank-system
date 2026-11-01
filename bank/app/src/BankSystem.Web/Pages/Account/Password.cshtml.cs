namespace BankSystem.Web.Pages.Account
{
    using Infrastructure.Extensions;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [Authorize]
    public class PasswordModel : BasePageModel
    {
        public IActionResult OnGet(string returnUrl = null)
            => this.RunKycAction(KycServiceCollectionExtensions.UpdatePasswordAction, returnUrl);
    }
}
