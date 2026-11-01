namespace BankSystem.Web.Pages
{
    using Common;
    using Infrastructure.Extensions;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;

    public abstract class BasePageModel : PageModel
    {
        public IActionResult RedirectToHome() => this.RedirectToAction("Index", "Home");

        public IActionResult RedirectToLoginPage()
            => this.RedirectToPage("/Account/Login");

        protected IActionResult ReturnTo(string returnUrl)
            => this.Url.IsLocalUrl(returnUrl) ? this.LocalRedirect(returnUrl) : this.RedirectToHome();

        /// <summary>Sends the user to Keycloak, which comes back here through /account/callback.</summary>
        protected IActionResult ChallengeKyc(string returnUrl, string prompt = null)
        {
            if (this.User.Identity.IsAuthenticated)
            {
                return this.ReturnTo(returnUrl);
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = this.Url.Page("/Account/Callback", new { returnUrl })
            };

            if (prompt != null)
            {
                properties.Items[KycServiceCollectionExtensions.PromptKey] = prompt;
            }

            return this.Challenge(properties, KycServiceCollectionExtensions.Scheme);
        }

        protected void ShowErrorMessage(string message)
        {
            this.TempData[GlobalConstants.TempDataErrorMessageKey] = message;
        }

        protected void ShowSuccessMessage(string message)
        {
            this.TempData[GlobalConstants.TempDataSuccessMessageKey] = message;
        }
    }
}
