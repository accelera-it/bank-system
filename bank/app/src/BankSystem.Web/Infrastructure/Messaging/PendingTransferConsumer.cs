namespace BankSystem.Web.Infrastructure.Messaging
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Helpers.GlobalTransferHelpers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    /// <summary>
    /// Delivers the transfers in the queue. An unknown outcome goes to the retry queue, which hands the
    /// message back after 30 seconds.
    /// </summary>
    public class PendingTransferConsumer : BackgroundService
    {
        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

        private readonly string connectionString;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IPendingTransferQueue queue;
        private readonly ILogger<PendingTransferConsumer> logger;

        public PendingTransferConsumer(
            string connectionString,
            IServiceScopeFactory scopeFactory,
            IPendingTransferQueue queue,
            ILogger<PendingTransferConsumer> logger)
        {
            this.connectionString = connectionString;
            this.scopeFactory = scopeFactory;
            this.queue = queue;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var factory = new ConnectionFactory { Uri = new Uri(this.connectionString) };
                    await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                    await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                    var closed = new TaskCompletionSource();
                    channel.ChannelShutdownAsync += (_, _) =>
                    {
                        closed.TrySetResult();
                        return Task.CompletedTask;
                    };

                    await channel.BasicQosAsync(0, 5, false, stoppingToken);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (_, message) =>
                    {
                        await this.HandleAsync(message.Body);
                        await channel.BasicAckAsync(message.DeliveryTag, false);
                    };

                    await channel.BasicConsumeAsync(
                        RabbitMqPendingTransferQueue.DeliverQueue, false, consumer, stoppingToken);
                    await closed.Task.WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "The transfer queue is not available");
                }

                await Task.Delay(ReconnectDelay, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
            }
        }

        private async Task HandleAsync(ReadOnlyMemory<byte> body)
        {
            var referenceNumber = JsonDocument.Parse(body).RootElement.GetProperty("ReferenceNumber").GetString();

            try
            {
                using var scope = this.scopeFactory.CreateScope();
                var keepTrying = await scope.ServiceProvider
                    .GetRequiredService<IGlobalTransferHelper>()
                    .DeliverPendingTransferAsync(referenceNumber);
                if (keepTrying)
                {
                    await this.queue.RetryLaterAsync(referenceNumber);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Delivering transfer {ReferenceNumber} failed", referenceNumber);
                await this.queue.RetryLaterAsync(referenceNumber);
            }
        }
    }
}
