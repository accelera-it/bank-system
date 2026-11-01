namespace BankSystem.Web.Infrastructure.Kyc
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using BankSystem.Models;
    using Microsoft.AspNetCore.Identity;

    public class KycUserService : IKycUserService
    {
        private readonly UserManager<BankUser> userManager;

        public KycUserService(UserManager<BankUser> userManager) => this.userManager = userManager;

        public async Task<BankUser> FindOrCreateAsync(string email, string fullName)
        {
            var user = await this.userManager.FindByEmailAsync(email);
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(fullName) && user.FullName != fullName)
                {
                    user.FullName = fullName;
                    await this.userManager.UpdateAsync(user);
                }

                return user;
            }

            user = new BankUser
            {
                UserName = email,
                Email = email,
                FullName = string.IsNullOrWhiteSpace(fullName) ? email : fullName,
                EmailConfirmed = true
            };

            var result = await this.userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            return user;
        }
    }
}
