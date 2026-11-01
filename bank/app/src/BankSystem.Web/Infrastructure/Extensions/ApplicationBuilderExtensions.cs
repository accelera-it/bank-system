namespace BankSystem.Web.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BankSystem.Models;
    using Common;
    using Data;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Middleware;

    public static class ApplicationBuilderExtensions
    {
        private const string AdministratorEmail = "demo@abs.uz";
        private const string PreviousAdministratorEmail = "admin@abs.uz";
        private const string AdministratorAccountId = "ABCJ98131785";

        public static IApplicationBuilder InitializeDatabase(this IApplicationBuilder app)
        {
            InitializeDatabaseAsync(app).GetAwaiter().GetResult();

            return app;
        }

        private static async Task InitializeDatabaseAsync(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<BankSystemDbContext>();
                var userManager = serviceScope.ServiceProvider.GetService<UserManager<BankUser>>();
                var roleManager = serviceScope.ServiceProvider.GetService<RoleManager<IdentityRole>>();

                if (dbContext.Database.IsRelational())
                {
                    await dbContext.Database.MigrateAsync();
                }
                else
                {
                    await dbContext.Database.EnsureCreatedAsync();
                }

                if (!await roleManager.RoleExistsAsync(GlobalConstants.AdministratorRoleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(GlobalConstants.AdministratorRoleName));
                }

                await SeedAdministrator(userManager, dbContext);
            }
        }

        private static async Task SeedAdministrator(UserManager<BankUser> userManager, BankSystemDbContext dbContext)
        {
            if (await userManager.FindByNameAsync(AdministratorEmail) != null)
            {
                return;
            }

            // A database seeded earlier holds the default user under its old email, with the shop's account
            var previous = await userManager.FindByNameAsync(PreviousAdministratorEmail);
            if (previous != null)
            {
                previous.UserName = AdministratorEmail;
                previous.Email = AdministratorEmail;
                await userManager.UpdateAsync(previous);
                return;
            }

            var user = new BankUser
            {
                Email = AdministratorEmail,
                FullName = "ABS Admin",
                UserName = AdministratorEmail,
                EmailConfirmed = true
            };

            // A database seeded earlier already holds this account, under the previous default user
            var account = await dbContext.Accounts.SingleOrDefaultAsync(a => a.UniqueId == AdministratorAccountId);
            if (account == null)
            {
                user.BankAccounts = new List<BankAccount>
                {
                    new BankAccount
                    {
                        Balance = 10_000_000,
                        CreatedOn = DateTime.UtcNow,
                        Name = "Main account",
                        UniqueId = AdministratorAccountId
                    }
                };
            }

            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            if (account != null)
            {
                account.UserId = user.Id;
                await dbContext.SaveChangesAsync();
            }

            await userManager.AddToRoleAsync(user, GlobalConstants.AdministratorRoleName);
        }

        public static IApplicationBuilder AddDefaultSecurityHeaders(
            this IApplicationBuilder app,
            SecurityHeadersBuilder builder)
            => app.UseMiddleware<SecurityHeadersMiddleware>(builder.Policy());
    }
}