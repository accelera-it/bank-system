namespace DemoShop.Web.Extensions
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Data;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using DemoShop.Models;

    public static class ApplicationBuilderExtensions
    {
        private const string DemoUserEmail = "demo@abs.uz";
        private const string DemoUserPassword = "123456";

        public static async Task InitializeDatabaseAsync(this IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<DemoShopDbContext>();
                var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<DemoShopUser>>();

                if (dbContext.Database.IsRelational())
                {
                    await dbContext.Database.MigrateAsync();
                }
                else
                {
                    await dbContext.Database.EnsureCreatedAsync();
                }

                await SeedUser(userManager);
                await SeedProduct(dbContext);
            }
        }

        private static async Task SeedUser(UserManager<DemoShopUser> userManager)
        {
            if (await userManager.FindByNameAsync(DemoUserEmail) != null)
            {
                return;
            }

            var user = new DemoShopUser
            {
                UserName = DemoUserEmail,
                Email = DemoUserEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, DemoUserPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }
        }

        private static async Task SeedProduct(DemoShopDbContext dbContext)
        {
            const string name = "Laptop 14\" Core i7, 16 GB, 512 GB SSD";

            if (await dbContext.Products.AnyAsync(p => p.Name == name))
            {
                return;
            }

            dbContext.Products.Add(new Product
            {
                Name = name,
                Price = 8_900_000,
                ImageUrl = "/img/products/laptop.svg"
            });

            await dbContext.SaveChangesAsync();
        }
    }
}
