namespace BankSystem.Data
{
    using Configurations;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using Models;

    public class BankSystemDbContext : IdentityDbContext<BankUser>
    {
        public BankSystemDbContext(DbContextOptions<BankSystemDbContext> options)
            : base(options)
        {
        }

        public DbSet<BankAccount> Accounts { get; set; }

        public DbSet<MoneyTransfer> Transfers { get; set; }

        public DbSet<PendingTransfer> PendingTransfers { get; set; }

        public DbSet<Card> Cards { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfiguration(new UserConfiguration());

            base.OnModelCreating(builder);

            // The filtered index below covers only credits, so account history needs its own
            builder.Entity<MoneyTransfer>().HasIndex(t => t.AccountId);
            builder.Entity<MoneyTransfer>().HasIndex(t => t.ReferenceNumber);

            // A bank must credit an incoming transfer once, however many times it is delivered
            builder.Entity<MoneyTransfer>()
                .HasIndex(t => new { t.AccountId, t.ReferenceNumber })
                .IsUnique()
                .HasFilter("[Amount] > 0");
        }
    }
}