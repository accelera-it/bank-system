namespace BankSystem.Web.Pages.Account
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [AllowAnonymous]
    public class LoginModel : BasePageModel
    {
        public IActionResult OnGet(string returnUrl = null) => this.ChallengeKyc(returnUrl);
    }
}
