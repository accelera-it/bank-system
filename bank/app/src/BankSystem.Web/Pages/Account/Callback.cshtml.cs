namespace BankSystem.Web.Pages.Account
{
    using System.Threading.Tasks;
    using BankSystem.Models;
    using Infrastructure.Kyc;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>Where Keycloak sends the user back: turns its login into a bank session.</summary>
    [AllowAnonymous]
    public class CallbackModel : BasePageModel
    {
        private const string IdToken = "id_token";

        private readonly IKycUserService kycUsers;
        private readonly SignInManager<BankUser> signInManager;

        public CallbackModel(IKycUserService kycUsers, SignInManager<BankUser> signInManager)
        {
            this.kycUsers = kycUsers;
            this.signInManager = signInManager;
        }

        public async Task<IActionResult> OnGetAsync(string returnUrl = null)
        {
            var login = await this.HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            var email = login.Principal?.FindFirst("email")?.Value;
            if (!login.Succeeded || string.IsNullOrEmpty(email))
            {
                return this.RedirectToHome();
            }

            var user = await this.kycUsers.FindOrCreateAsync(email, login.Principal.FindFirst("name")?.Value);

            var properties = new AuthenticationProperties();
            properties.StoreTokens(new[]
            {
                new AuthenticationToken { Name = IdToken, Value = login.Properties.GetTokenValue(IdToken) }
            });

            await this.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            await this.signInManager.SignInAsync(user, properties);

            return this.ReturnTo(returnUrl);
        }
    }
}
