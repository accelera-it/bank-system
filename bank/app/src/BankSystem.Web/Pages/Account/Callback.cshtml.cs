namespace BankSystem.Web.Pages.Account
{
    using System.Linq;
    using System.Threading.Tasks;
    using BankSystem.Models;
    using Common.Configuration;
    using Infrastructure.Extensions;
    using Infrastructure.Kyc;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    /// <summary>Where Keycloak sends the user back: turns its login into a bank session.</summary>
    [AllowAnonymous]
    public class CallbackModel : BasePageModel
    {
        private readonly IKycUserService kycUsers;
        private readonly SignInManager<BankUser> signInManager;
        private readonly KycConfiguration kyc;

        public CallbackModel(
            IKycUserService kycUsers,
            SignInManager<BankUser> signInManager,
            IOptions<KycConfiguration> kyc)
        {
            this.kycUsers = kycUsers;
            this.signInManager = signInManager;
            this.kyc = kyc.Value;
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
                KycTokenRefresher.IdToken, KycTokenRefresher.RefreshToken, KycTokenRefresher.ExpiresAt
            }.Select(name => new AuthenticationToken { Name = name, Value = login.Properties.GetTokenValue(name) }));

            await this.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // A changed profile or password ends the session everywhere, so the user signs in again
            if (login.Properties.Items.TryGetValue(KycServiceCollectionExtensions.ActionStatusKey, out var status) &&
                status == KycServiceCollectionExtensions.ActionSucceeded)
            {
                await this.signInManager.SignOutAsync();
                return this.Redirect(KycUrls.Logout(
                    this.kyc,
                    login.Properties.GetTokenValue(KycTokenRefresher.IdToken),
                    $"{this.Request.Scheme}://{this.Request.Host}/relogin.html"));
            }

            await this.signInManager.SignInAsync(user, properties);

            return this.ReturnTo(returnUrl);
        }
    }
}
