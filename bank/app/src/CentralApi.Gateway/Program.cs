namespace CentralApi.Gateway
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Ocelot.DependencyInjection;
    using Ocelot.Middleware;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddOcelot(builder.Configuration);

            var app = builder.Build();

            // Ocelot ends the pipeline, so the gateway's own liveness answer has to come first.
            app.MapWhen(
                context => context.Request.Path == "/health",
                health => health.Run(context => context.Response.WriteAsync("ok")));

            await app.UseOcelot();
            await app.RunAsync();
        }
    }
}
