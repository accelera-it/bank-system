namespace BankSystem.Web.Pages.Account
{
    using BankSystem.Models;
    using Common;
    using Common.EmailSender.Interface;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Models;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;

    [AllowAnonymous]
    public class RegisterModel : BasePageModel
    {
        private readonly IEmailSender emailSender;
        private readonly ILogger<RegisterModel> logger;
        private readonly UserManager<BankUser> userManager;

        public RegisterModel(
            UserManager<BankUser> userManager,
            ILogger<RegisterModel> logger, IEmailSender emailSender)
        {
            this.userManager = userManager;
            this.logger = logger;
            this.emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IActionResult OnGet(string returnUrl = null)
        {
            returnUrl = returnUrl ?? this.Url.Content("~/");

            if (this.User.Identity.IsAuthenticated)
            {
                return this.LocalRedirect(returnUrl);
            }

            return this.RedirectToAuthModal(returnUrl);
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? this.Url.Content("~/");

            if (this.User.Identity.IsAuthenticated)
            {
                return this.LocalRedirect(returnUrl);
            }

            if (!this.ModelState.IsValid)
            {
                this.ShowErrorMessage(string.Join(" ", this.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)));
                return this.RedirectToAuthModal(returnUrl);
            }

            var user = new BankUser
            {
                UserName = this.Input.Email,
                Email = this.Input.Email,
                FullName = this.Input.FullName
            };

            var result = await this.userManager.CreateAsync(user, this.Input.Password);
            if (!result.Succeeded)
            {
                this.ShowErrorMessage(string.Join(" ", result.Errors.Select(e => e.Description)));
                return this.RedirectToAuthModal(returnUrl);
            }

            this.logger.LogInformation("User created a new account with password.");

            var code = await this.userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = this.Url.Page(
                EmailMessages.EmailConfirmationPage,
                null,
                new { userId = user.Id, code },
                this.Request.Scheme);
            await this.emailSender.SendEmailAsync(GlobalConstants.BankSystemEmail, this.Input.Email,
                EmailMessages.ConfirmEmailSubject,
                string.Format(EmailMessages.EmailConfirmationMessage, HtmlEncoder.Default.Encode(callbackUrl)));

            this.ShowSuccessMessage(NotificationMessages.SuccessfulRegistration);
            return this.RedirectToLoginPage();
        }

        private IActionResult RedirectToAuthModal(string returnUrl)
            => this.RedirectToAction("Index", "Home", new
            {
                area = string.Empty,
                auth = "register",
                returnUrl = returnUrl == this.Url.Content("~/") ? null : returnUrl
            });

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [MaxLength(ModelConstants.User.FullNameMaxLength)]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required]
            [StringLength(ModelConstants.User.PasswordMaxLength, MinimumLength = ModelConstants.User.PasswordMinLength)]
            [DataType(DataType.Password)]
            [RegularExpression(ModelConstants.User.PasswordRegex,
                ErrorMessage = ModelConstants.User.PasswordErrorMessage)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }
    }
}