namespace BankSystem.Web.Pages.Account.Settings
{
    using System.Threading.Tasks;
    using BankSystem.Models;
    using Common.Configuration;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    [Authorize]
    public class IndexModel : BasePageModel
    {
        private readonly UserManager<BankUser> userManager;
        private readonly KycConfiguration kyc;

        public IndexModel(UserManager<BankUser> userManager, IOptions<KycConfiguration> kyc)
        {
            this.userManager = userManager;
            this.kyc = kyc.Value;
        }

        public string Email { get; set; }

        public string FullName { get; set; }

        public string AccountUrl => $"{this.kyc.PublicAuthority}/account";

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await this.userManager.GetUserAsync(this.User);
            if (user == null)
            {
                return this.Forbid();
            }

            this.FullName = user.FullName;
            this.Email = user.Email;

            return this.Page();
        }
    }
}
