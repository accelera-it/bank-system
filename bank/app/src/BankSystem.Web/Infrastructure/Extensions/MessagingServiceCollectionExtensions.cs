namespace BankSystem.Web.Infrastructure.Extensions
{
    using Messaging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public static class MessagingServiceCollectionExtensions
    {
        /// <summary>
        /// Without a connection string (tests, plain dotnet run) there is no broker, and
        /// PendingTransferRetryService does the retrying alone.
        /// </summary>
        public static IServiceCollection AddPendingTransferQueue(this IServiceCollection services, string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return services.AddSingleton<IPendingTransferQueue, NullPendingTransferQueue>();
            }

            services.AddSingleton<IPendingTransferQueue>(provider => new RabbitMqPendingTransferQueue(
                connectionString, provider.GetRequiredService<ILogger<RabbitMqPendingTransferQueue>>()));
            services.AddSingleton<IHostedService>(provider => new PendingTransferConsumer(
                connectionString,
                provider.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
                provider.GetRequiredService<IPendingTransferQueue>(),
                provider.GetRequiredService<ILogger<PendingTransferConsumer>>()));

            return services;
        }
    }
}
