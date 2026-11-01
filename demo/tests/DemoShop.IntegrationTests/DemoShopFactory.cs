namespace DemoShop.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BankSystem.Tests.Shared;
    using Data;
    using DemoShop.Models;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Web;

    public class DemoShopFactory : WebApplicationFactory<Startup>
    {
        public const string UserName = "buyer@test.com";
        public const string ProductName = "Test product";

        private readonly string databaseName = TestDatabase.CreateName("DemoShopTests");

        public DemoPaymentChain Chain { get; } = new DemoPaymentChain();

        public StubHttpServer CentralApi { get; } = new StubHttpServer();

        public async Task<(string UserId, string ProductId)> EnsureSeedAsync()
        {
            using var scope = this.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DemoShopDbContext>();

            var user = await db.Users.SingleOrDefaultAsync(u => u.UserName == UserName);
            if (user == null)
            {
                user = new DemoShopUser { UserName = UserName, NormalizedUserName = UserName.ToUpperInvariant() };
                db.Users.Add(user);
            }

            var product = await db.Products.SingleOrDefaultAsync(p => p.Name == ProductName);
            if (product == null)
            {
                product = new Product { Name = ProductName, Price = 15, ImageUrl = "https://shop.test/image.png" };
                db.Products.Add(product);
            }

            await db.SaveChangesAsync();

            return (user.Id, product.Id);
        }

        public async Task<string> CreateOrderAsync()
        {
            var (userId, productId) = await this.EnsureSeedAsync();

            using var scope = this.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DemoShopDbContext>();
            var order = new Order
            {
                UserId = userId,
                ProductId = productId,
                PaymentStatus = PaymentStatus.Pending,
                CreatedOn = DateTime.UtcNow
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            return order.Id;
        }

        public async Task<PaymentStatus> GetOrderStatusAsync(string orderId)
        {
            using var scope = this.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DemoShopDbContext>();

            return (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).PaymentStatus;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["DirectPaymentsConfiguration:SiteKey"] = this.Chain.Site.Private,
                ["DirectPaymentsConfiguration:CentralApiPublicKey"] = this.Chain.CentralApi.Public,
                ["DirectPaymentsConfiguration:CentralApiPaymentUrl"] = this.CentralApi.Url + "pay",
                ["CardPaymentsConfiguration:CentralApiCardPaymentsUrl"] = this.CentralApi.Url
            }));
            builder.ConfigureTestServices(services =>
            {
                services.UseTestDatabase<DemoShopDbContext>(this.databaseName);
                services.AddTestAuthentication();
            });
        }

        public override async ValueTask DisposeAsync()
        {
            TestDatabase.Delete<DemoShopDbContext>(this.Services);
            this.CentralApi.Dispose();

            await base.DisposeAsync();
        }
    }
}
