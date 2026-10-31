namespace BankSystem.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BankSystem.Models;
    using BankSystem.Tests.Shared;
    using Common.EmailSender.Interface;
    using Data;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Moq;
    using Web;

    public class BankWebFactory : WebApplicationFactory<Startup>
    {
        public const string SeededAccountUniqueId = "ABCJ98131785";
        public const string SeededUserName = "admin@abs.uz";
        public const string SeededUserFullName = "ABS Admin";

        private readonly string databaseName = TestDatabase.CreateName("BankTests");

        public (string PublicKey, string PrivateKey) BankKeys { get; } = SecureRequestBuilder.GenerateKeyPair();

        public (string PublicKey, string PrivateKey) CentralApiKeys { get; } = SecureRequestBuilder.GenerateKeyPair();

        public StubHttpServer CentralApi { get; } = new StubHttpServer();

        public async Task<T> WithDbAsync<T>(Func<BankSystemDbContext, Task<T>> action)
        {
            using var scope = this.Services.CreateScope();

            return await action(scope.ServiceProvider.GetRequiredService<BankSystemDbContext>());
        }

        public Task<BankAccount> GetSeededAccountAsync()
            => this.WithDbAsync(db => db.Accounts.AsNoTracking().Include(a => a.User)
                .SingleAsync(a => a.UniqueId == SeededAccountUniqueId));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["BankConfiguration:Key"] = this.BankKeys.PrivateKey,
                ["BankConfiguration:CentralApiPublicKey"] = this.CentralApiKeys.PublicKey,
                ["BankConfiguration:CentralApiAddress"] = this.CentralApi.Url
            }));
            builder.ConfigureTestServices(services =>
            {
                services.UseTestDatabase<BankSystemDbContext>(this.databaseName);
                services.AddTestAuthentication();
                services.RemoveAll<IEmailSender>();
                services.AddSingleton(Mock.Of<IEmailSender>());
            });
        }

        public override async ValueTask DisposeAsync()
        {
            TestDatabase.Delete<BankSystemDbContext>(this.Services);
            this.CentralApi.Dispose();

            await base.DisposeAsync();
        }
    }
}
