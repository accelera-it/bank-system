namespace CentralApi.Gateway
{
    using System;
    using System.Threading.RateLimiting;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Ocelot.DependencyInjection;
    using Ocelot.Middleware;

    public class Program
    {
        private const string ClientAddressHeader = "X-Real-IP";

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddOcelot(builder.Configuration);

            var limit = builder.Configuration.GetSection("RateLimit").Get<RateLimit>() ?? new RateLimit();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    // Only nginx sets this header: calls from inside the Docker network are not limited
                    var client = context.Request.Headers[ClientAddressHeader].ToString();
                    return string.IsNullOrEmpty(client)
                        ? RateLimitPartition.GetNoLimiter(string.Empty)
                        : RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limit.PermitLimit,
                            Window = TimeSpan.FromSeconds(limit.WindowSeconds)
                        });
                });
            });

            var app = builder.Build();

            // Ocelot ends the pipeline, so the gateway's own liveness answer has to come first.
            app.MapWhen(
                context => context.Request.Path == "/health",
                health => health.Run(context => context.Response.WriteAsync("ok")));

            app.UseRateLimiter();

            await app.UseOcelot();
            await app.RunAsync();
        }

        private sealed class RateLimit
        {
            public int PermitLimit { get; set; } = 60;

            public int WindowSeconds { get; set; } = 60;
        }
    }
}
