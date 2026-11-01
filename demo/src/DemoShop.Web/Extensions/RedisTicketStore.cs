namespace DemoShop.Web.Extensions
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.Extensions.Caching.Distributed;

    /// <summary>
    /// Keeps login tickets server side, in the distributed cache (Redis). The cookie only carries the key.
    /// </summary>
    public class RedisTicketStore : ITicketStore
    {
        private const string KeyPrefix = "auth-ticket:";

        private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(14);

        private readonly IDistributedCache cache;

        public RedisTicketStore(IDistributedCache cache)
            => this.cache = cache;

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var key = KeyPrefix + Guid.NewGuid().ToString("N");
            await this.RenewAsync(key, ticket);

            return key;
        }

        public Task RenewAsync(string key, AuthenticationTicket ticket)
            => this.cache.SetAsync(
                key,
                TicketSerializer.Default.Serialize(ticket),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.Add(DefaultLifetime),
                });

        public async Task<AuthenticationTicket> RetrieveAsync(string key)
        {
            var bytes = await this.cache.GetAsync(key);

            return bytes == null ? null : TicketSerializer.Default.Deserialize(bytes);
        }

        public Task RemoveAsync(string key)
            => this.cache.RemoveAsync(key);
    }
}
