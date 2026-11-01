namespace BankSystem.Tests.Shared
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Points an app at an isolated EF Core InMemory database.
    /// </summary>
    public static class TestDatabase
    {
        public static string CreateName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        public static IServiceCollection UseTestDatabase<TContext>(this IServiceCollection services, string name)
            where TContext : DbContext
        {
            services.RemoveAll<DbContextOptions<TContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();

            // Singleton options also suit the apps that register a pooled context
            services.AddDbContext<TContext>(
                options => options.UseInMemoryDatabase(name), ServiceLifetime.Scoped, ServiceLifetime.Singleton);

            return services;
        }

        public static void Delete<TContext>(IServiceProvider services)
            where TContext : DbContext
        {
            using var scope = services.CreateScope();
            scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureDeleted();
        }
    }
}
