namespace BankSystem.Web.Pages.Account
{
    using System.Threading.Tasks;
    using BankSystem.Models;
    using Common.Configuration;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System.Collections.Generic;

    [AllowAnonymous]
    public class LogoutModel : BasePageModel
    {
        private readonly ILogger<LogoutModel> logger;
        private readonly SignInManager<BankUser> signInManager;
        private readonly KycConfiguration kyc;

        public LogoutModel(
            SignInManager<BankUser> signInManager,
            ILogger<LogoutModel> logger,
            IOptions<KycConfiguration> kyc)
        {
            this.signInManager = signInManager;
            this.logger = logger;
            this.kyc = kyc.Value;
        }

        public IActionResult OnGet()
            => !this.User.Identity.IsAuthenticated ? this.RedirectToHome() : this.Page();

        public async Task<IActionResult> OnPost()
        {
            if (!this.User.Identity.IsAuthenticated)
            {
                return this.RedirectToHome();
            }

            var idToken = await this.HttpContext.GetTokenAsync(IdentityConstants.ApplicationScheme, "id_token");
            await this.signInManager.SignOutAsync();
            this.logger.LogInformation("User logged out.");

            if (string.IsNullOrEmpty(idToken))
            {
                return this.RedirectToHome();
            }

            // Ends the Keycloak session too, which comes back to the bank's home page
            return this.Redirect(QueryHelpers.AddQueryString(
                $"{this.kyc.PublicAuthority}/protocol/openid-connect/logout",
                new Dictionary<string, string>
                {
                    ["id_token_hint"] = idToken,
                    ["post_logout_redirect_uri"] = $"{this.Request.Scheme}://{this.Request.Host}/"
                }));
        }
    }
}
