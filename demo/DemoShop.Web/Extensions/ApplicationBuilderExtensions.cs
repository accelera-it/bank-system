namespace DemoShop.Web.Extensions
{
    using System.Threading.Tasks;
    using Data;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public static class ApplicationBuilderExtensions
    {
        public static async Task InitializeDatabaseAsync(this IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<DemoShopDbContext>();

                if (dbContext.Database.IsRelational())
                {
                    await dbContext.Database.MigrateAsync();
                }
                else
                {
                    await dbContext.Database.EnsureCreatedAsync();
                }
            }
        }
    }
}