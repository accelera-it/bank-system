namespace BankSystem.Tests.Shared
{
    using System;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>
    /// Points an app at an isolated database: the SQL Server in TEST_SQL_CONNECTION when it is set
    /// (docker compose), otherwise EF Core InMemory (plain dotnet test).
    /// </summary>
    public static class TestDatabase
    {
        private const string ConnectionVariable = "TEST_SQL_CONNECTION";

        public static string CreateName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

        public static IServiceCollection UseTestDatabase<TContext>(this IServiceCollection services, string name)
            where TContext : DbContext
        {
            services.RemoveAll<DbContextOptions<TContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();

            // Singleton options also suit the apps that register a pooled context
            services.AddDbContext<TContext>(
                options => Configure(options, name), ServiceLifetime.Scoped, ServiceLifetime.Singleton);

            return services;
        }

        public static void Delete<TContext>(IServiceProvider services)
            where TContext : DbContext
        {
            using var scope = services.CreateScope();
            scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureDeleted();
        }

        private static void Configure(DbContextOptionsBuilder options, string name)
        {
            var connection = Environment.GetEnvironmentVariable(ConnectionVariable);

            if (string.IsNullOrEmpty(connection))
            {
                options.UseInMemoryDatabase(name);
                return;
            }

            options.UseSqlServer(new SqlConnectionStringBuilder(connection) { InitialCatalog = name }.ConnectionString);
        }
    }
}
