namespace DemoShop.Web.Extensions
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.DependencyInjection;
    using StackExchange.Redis;

    public static class RedisServiceCollectionExtensions
    {
        /// <summary>
        /// Puts IDistributedCache, login tickets and Data Protection keys in Redis.
        /// Without a connection string (tests, plain dotnet run) the cache lives in memory.
        /// </summary>
        public static IServiceCollection AddRedisSessions(
            this IServiceCollection services, string connectionString, string applicationName)
        {
            var dataProtection = services.AddDataProtection().SetApplicationName(applicationName);

            if (string.IsNullOrEmpty(connectionString))
            {
                services.AddDistributedMemoryCache();
            }
            else
            {
                var redis = ConnectionMultiplexer.Connect(connectionString);

                dataProtection.PersistKeysToStackExchangeRedis(redis, $"{applicationName}:data-protection");
                services.AddStackExchangeRedisCache(options =>
                {
                    options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redis);
                    options.InstanceName = $"{applicationName}:";
                });
            }

            services.AddSingleton<ITicketStore, RedisTicketStore>();
            services
                .AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
                .Configure<ITicketStore>((options, store) => options.SessionStore = store);

            return services;
        }
    }
}
