namespace BankSystem.Web.Pages.Account
{
    using BankSystem.Models;
    using Common;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Models;
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;

    [AllowAnonymous]
    public class RegisterModel : BasePageModel
    {
        private readonly ILogger<RegisterModel> logger;
        private readonly SignInManager<BankUser> signInManager;
        private readonly UserManager<BankUser> userManager;

        public RegisterModel(
            UserManager<BankUser> userManager,
            SignInManager<BankUser> signInManager,
            ILogger<RegisterModel> logger)
        {
            this.userManager = userManager;
            this.logger = logger;
            this.signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; private set; }

        public IActionResult OnGet(string returnUrl = null)
        {
            if (this.User.Identity.IsAuthenticated)
            {
                return this.LocalRedirect(returnUrl ?? this.Url.Content("~/"));
            }

            this.ReturnUrl = returnUrl;
            return this.Page();
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
                return this.Page();
            }

            var user = new BankUser
            {
                UserName = this.Input.Email,
                Email = this.Input.Email,
                FullName = this.Input.FullName,
                EmailConfirmed = true
            };

            var result = await this.userManager.CreateAsync(user, this.Input.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    this.ModelState.AddModelError(string.Empty, error.Description);
                }

                return this.Page();
            }

            this.logger.LogInformation("User created a new account with password.");

            await this.signInManager.SignInAsync(user, false);

            return this.LocalRedirect(returnUrl);
        }

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

            [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms of the agreement.")]
            public bool AcceptAgreement { get; set; }
        }
    }
}