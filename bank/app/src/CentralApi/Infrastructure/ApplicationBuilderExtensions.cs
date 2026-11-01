namespace CentralApi.Infrastructure
{
    using System.Linq;
    using Data;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Models;

    public static class ApplicationBuilderExtensions
    {
        private const string DefaultBankBaseUrlKey = "DefaultBankBaseUrl";
        private const string DefaultBankPublicUrlKey = "DefaultBankPublicUrl";
        private const string DefaultBankBaseUrl = "https://localhost:56013";
        private const string DefaultBankSwiftCode = "ABC";

        public static void SeedData(this IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetService<CentralApiDbContext>();
                if (dbContext.Database.IsRelational())
                {
                    dbContext.Database.Migrate();
                }
                else
                {
                    dbContext.Database.EnsureCreated();
                }

                // Seed data
                var configuration = serviceScope.ServiceProvider.GetRequiredService<IConfiguration>();
                var bankBaseUrl = configuration[DefaultBankBaseUrlKey] ?? DefaultBankBaseUrl;

                // Browsers open the payment page, so it can differ from the address services use
                var bankPublicUrl = configuration[DefaultBankPublicUrlKey] ?? bankBaseUrl;
                SeedAvailableBanks(dbContext, bankBaseUrl, bankPublicUrl);
            }
        }

        private static void SeedAvailableBanks(
            CentralApiDbContext dbContext, string bankBaseUrl, string bankPublicUrl)
        {
            var paymentUrl = $"{bankPublicUrl}/pay";
            var defaultBank = dbContext.Banks.SingleOrDefault(b => b.SwiftCode == DefaultBankSwiftCode);

            if (defaultBank != null)
            {
                defaultBank.PaymentUrl = paymentUrl;
                defaultBank.ApiAddress = $"{bankBaseUrl}/api/ReceiveMoneyTransfers";
                defaultBank.CardPaymentUrl = $"{bankBaseUrl}/api/cardPayments";
                dbContext.SaveChanges();
            }

            if (!dbContext.Banks.Any())
            {
                dbContext.AddRange(
                    new Bank
                    {
                        Location = "Uzbekistan",
                        Name = "Bank system",
                        SwiftCode = DefaultBankSwiftCode,
                        ApiKey =
                            "<RSAKeyValue><Modulus>uBJGDt7UVg068eAXtaJ8wxbTLtxJWubChoTCCljt4t8eCcUQTjbVjmiX4n9q01PyfP3Xe2MqKx0HhSygSQr4GTsPhUo44EEJr9E6ZgAjQcBJTbRop4i06BFk+u0x0P/9nZtjQQqFhKrTdVPP9rSc8CYYDsEVzsQTtjKZdtUkPFITfz6fwJHQm2Zswqwu9sSlIIwknz0jKG7lnbEwxrqQy57jjxM7ZujYCop+N20KvBIHLquuH3wkTIxmj4nZLscYBbAE7J4JhuHVE/fCJFIA+M9JgKVvEHeC5HIqshMSaAGRv9Th6HHk0v4QFm3NdpZsVf2Qhu0iOap3fHHoAypFZQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>",
                        ApiAddress = $"{bankBaseUrl}/api/ReceiveMoneyTransfers",
                        PaymentUrl = paymentUrl,
                        CardPaymentUrl = $"{bankBaseUrl}/api/cardPayments",
                        BankIdentificationCardNumbers = "101"
                    }
                );
                dbContext.SaveChanges();
            }
        }
    }
}