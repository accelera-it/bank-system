namespace CentralApi
{
    using System;
    using BankSystem.Common.Mapping;
    using BankSystem.Common.Utils;
    using Data;
    using Infrastructure;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Services.Bank;

    public class Startup
    {
        public Startup(IConfiguration configuration)
            => this.Configuration = configuration;

        private IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHealthChecks();

            services.AddDbContext<CentralApiDbContext>(options =>
                options.UseSqlServer(
                    this.Configuration.GetConnectionString("CentralApi")));
            services.AddScoped<IBanksService, BanksService>();

            services.Configure<CentralApiConfiguration>(
                this.Configuration.GetSection(nameof(CentralApiConfiguration)));

            services.PostConfigure<CentralApiConfiguration>(settings =>
            {
                if (!ValidationUtil.IsObjectValid(settings))
                {
                    throw new ApplicationException("CentralApiConfiguration is invalid");
                }
            });
            services.AddMapping(typeof(Startup).Assembly);
            services
                .AddControllersWithViews();

            services.AddSwaggerGen();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Only Ocelot can reach this app in Docker. It sends the /api prefix of the pay pages, so links keep it.
            var forwardedHeaders = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedPrefix };
            forwardedHeaders.KnownIPNetworks.Clear();
            forwardedHeaders.KnownProxies.Clear();
            app.UseForwardedHeaders(forwardedHeaders);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });

            app.SeedData();
        }
    }
}