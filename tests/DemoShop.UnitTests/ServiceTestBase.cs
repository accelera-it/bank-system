namespace DemoShop.UnitTests
{
    using System;
    using Data;
    using Microsoft.EntityFrameworkCore;

    public abstract class ServiceTestBase
    {
        protected ServiceTestBase()
        {
            var options = new DbContextOptionsBuilder<DemoShopDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            this.Context = new DemoShopDbContext(options);
        }

        protected DemoShopDbContext Context { get; }
    }
}
