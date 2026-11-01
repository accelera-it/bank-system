namespace BankSystem.Web.Infrastructure.Messaging
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using RabbitMQ.Client;

    public sealed class RabbitMqPendingTransferQueue : IPendingTransferQueue, IAsyncDisposable
    {
        public const string DeliverQueue = "transfers.deliver";
        private const string RetryQueue = "transfers.retry";
        private const string SettledExchange = "payments.settled";

        private readonly string connectionString;
        private readonly ILogger<RabbitMqPendingTransferQueue> logger;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private IConnection connection;
        private IChannel channel;

        public RabbitMqPendingTransferQueue(string connectionString, ILogger<RabbitMqPendingTransferQueue> logger)
        {
            this.connectionString = connectionString;
            this.logger = logger;
        }

        public Task EnqueueAsync(string referenceNumber) => this.PublishAsync(string.Empty, DeliverQueue, referenceNumber);

        public Task RetryLaterAsync(string referenceNumber) => this.PublishAsync(string.Empty, RetryQueue, referenceNumber);

        public Task NotifySettledAsync(string referenceNumber)
            => this.PublishAsync(SettledExchange, string.Empty, referenceNumber);

        public async ValueTask DisposeAsync()
        {
            this.channel?.Dispose();
            this.connection?.Dispose();
            await Task.CompletedTask;
        }

        // A broker that is away must not fail a payment: the database has the transfer
        private async Task PublishAsync(string exchange, string routingKey, string referenceNumber)
        {
            await this.gate.WaitAsync();

            try
            {
                var body = JsonSerializer.SerializeToUtf8Bytes(new { ReferenceNumber = referenceNumber });
                var open = await this.GetChannelAsync();
                await open.BasicPublishAsync(
                    exchange, routingKey, false, new BasicProperties { Persistent = true }, body);
            }
            catch (Exception ex)
            {
                this.channel = null;
                this.logger.LogWarning(ex, "Message for transfer {ReferenceNumber} was not sent", referenceNumber);
            }
            finally
            {
                this.gate.Release();
            }
        }

        private async Task<IChannel> GetChannelAsync()
        {
            if (this.channel is { IsOpen: true })
            {
                return this.channel;
            }

            if (this.connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory { Uri = new Uri(this.connectionString) };
                this.connection = await factory.CreateConnectionAsync();
            }

            this.channel = await this.connection.CreateChannelAsync(
                new CreateChannelOptions(true, true));

            return this.channel;
        }
    }
}
