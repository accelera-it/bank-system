namespace CentralApi.IntegrationTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BankSystem.Tests.Shared;
    using CentralApi.Models;
    using Data;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class CentralApiFactory : WebApplicationFactory<Startup>
    {
        public const string BankName = "Test bank";
        public const string BankSwiftCode = "TST";
        public const string BankCountry = "Testland";
        public const string BankCardPrefix = "202";

        private readonly string databaseName = TestDatabase.CreateName("CentralApiTests");

        public (string PublicKey, string PrivateKey) CentralApiKeys { get; } = SecureRequestBuilder.GenerateKeyPair();

        public (string PublicKey, string PrivateKey) BankKeys { get; } = SecureRequestBuilder.GenerateKeyPair();

        public StubHttpServer BankServer { get; } = new StubHttpServer();

        public async Task<string> EnsureTestBankAsync()
        {
            using var scope = this.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CentralApiDbContext>();
            var bank = await db.Banks.SingleOrDefaultAsync(b => b.SwiftCode == BankSwiftCode);

            if (bank == null)
            {
                bank = new Bank
                {
                    Name = BankName,
                    SwiftCode = BankSwiftCode,
                    Location = BankCountry,
                    ApiKey = this.BankKeys.PublicKey,
                    ApiAddress = this.BankServer.Url,
                    PaymentUrl = this.BankServer.Url + "pay",
                    CardPaymentUrl = this.BankServer.Url,
                    BankIdentificationCardNumbers = BankCardPrefix
                };
                db.Banks.Add(bank);
                await db.SaveChangesAsync();
            }

            return bank.Id;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CentralApiConfiguration:Key"] = this.CentralApiKeys.PrivateKey
            }));
            builder.ConfigureTestServices(
                services => services.UseTestDatabase<CentralApiDbContext>(this.databaseName));
        }

        public override async ValueTask DisposeAsync()
        {
            TestDatabase.Delete<CentralApiDbContext>(this.Services);
            this.BankServer.Dispose();

            await base.DisposeAsync();
        }
    }
}
