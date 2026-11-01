namespace BankSystem.Web.Pages.Account
{
    using Infrastructure.Extensions;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [AllowAnonymous]
    public class RegisterModel : BasePageModel
    {
        public IActionResult OnGet(string returnUrl = null)
            => this.ChallengeKyc(returnUrl, KycServiceCollectionExtensions.RegisterPrompt);
    }
}
