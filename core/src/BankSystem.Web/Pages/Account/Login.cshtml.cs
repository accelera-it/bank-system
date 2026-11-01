namespace BankSystem.Web.Pages.Account
{
    using BankSystem.Models;
    using Common;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using System.ComponentModel.DataAnnotations;
    using System.Security.Claims;
    using System.Threading.Tasks;

    [AllowAnonymous]
    public class LoginModel : BasePageModel
    {

        private readonly ILogger<LoginModel> logger;
        private readonly SignInManager<BankUser> signInManager;

        public LoginModel(
            SignInManager<BankUser> signInManager,
            ILogger<LoginModel> logger)
        {
            this.signInManager = signInManager;
            this.logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public async Task<IActionResult> OnGetAsync(string returnUrl = null)
        {
            // Session has expired
            if (returnUrl != null)
            {
                this.ShowErrorMessage(NotificationMessages.SessionExpired);
            }

            returnUrl ??= this.Url.Content("~/");

            if (this.User.Identity.IsAuthenticated)
            {
                return this.LocalRedirect(returnUrl);
            }

            // Clear the existing external cookie to ensure a clean login process
            await this.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            return this.RedirectToAuthModal(returnUrl);
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= this.Url.Content("~/");

            if (this.User.Identity.IsAuthenticated)
            {
                return this.LocalRedirect(returnUrl);
            }

            if (!this.ModelState.IsValid)
            {
                this.ShowErrorMessage(NotificationMessages.InvalidCredentials);
                return this.RedirectToAuthModal(returnUrl);
            }

            var result = await this.signInManager.PasswordSignInAsync(this.Input.Email, this.Input.Password,
                false, true);

            if (!result.Succeeded)
            {
                if (result.RequiresTwoFactor)
                {
                    return this.RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = false });
                }

                if (result.IsLockedOut)
                {
                    this.ShowErrorMessage(NotificationMessages.LoginLockedOut);
                    return this.RedirectToAuthModal(returnUrl);
                }

                this.ShowErrorMessage(NotificationMessages.InvalidCredentials);
                return this.RedirectToAuthModal(returnUrl);
            }

            var user = await this.signInManager.UserManager.FindByNameAsync(this.Input.Email);

            if (!user.TwoFactorEnabled && !this.Request.Cookies.ContainsKey(GlobalConstants.IgnoreTwoFactorWarningCookie))
            {
                this.TempData.Add(GlobalConstants.TempDataNoTwoFactorKey, true);
            }
            
            this.AddClaims(user);

            this.logger.LogInformation("User logged in.");
            return this.LocalRedirect(returnUrl);
        }

        private void AddClaims(BankUser user)
        {
            var claims = new Claim[2]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email)
            };
            this.User.AddIdentity(new ClaimsIdentity(claims));
        }

        private IActionResult RedirectToAuthModal(string returnUrl)
            => this.RedirectToAction("Index", "Home", new
            {
                area = string.Empty,
                auth = "login",
                returnUrl = returnUrl == this.Url.Content("~/") ? null : returnUrl
            });

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }
    }
}