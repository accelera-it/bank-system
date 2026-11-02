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

            // A pending transfer's JSON has no upper limit, and NVARCHAR2 stops at 2000 characters
            builder.Entity<PendingTransfer>().Property(p => p.Payload).HasColumnType("NCLOB");

            builder.Entity<MoneyTransfer>().HasIndex(t => t.AccountId);
            builder.Entity<MoneyTransfer>().HasIndex(t => t.ReferenceNumber);

            // A bank must credit an incoming transfer once, however many times it is delivered.
            // Oracle has no filtered indexes, so the Initial migration creates that unique index
            // on expressions that are NULL for debits, which leaves them out of it.
        }
    }
}