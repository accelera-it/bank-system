namespace BankSystem.Web.Infrastructure.Helpers.GlobalTransferHelpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Finishes global transfers whose outcome is unknown, such as after a timeout or a restart.
    /// </summary>
    public class PendingTransferRetryService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        // Longer than a request may take, to leave transfers in progress to their own request
        private static readonly TimeSpan MinimumAge = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<PendingTransferRetryService> logger;

        public PendingTransferRetryService(
            IServiceScopeFactory scopeFactory,
            ILogger<PendingTransferRetryService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        using var scope = this.scopeFactory.CreateScope();
                        await scope.ServiceProvider
                            .GetRequiredService<IGlobalTransferHelper>()
                            .RetryPendingTransfersAsync(MinimumAge);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        this.logger.LogError(ex, "Retrying pending transfers failed");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The app is stopping
            }
        }
    }
}
