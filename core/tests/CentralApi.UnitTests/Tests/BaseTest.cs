namespace CentralApi.Services.UnitTests.Tests
{
    using System;
    using MapsterMapper;
    using Data;
    using Microsoft.EntityFrameworkCore;
    using Setup;

    public abstract class BaseTest
    {
        protected BaseTest()
            => this.Mapper = TestSetup.InitializeMapper();

        protected IMapper Mapper { get; }

        protected CentralApiDbContext DatabaseInstance
        {
            get
            {
                var options = new DbContextOptionsBuilder<CentralApiDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .EnableSensitiveDataLogging()
                    .Options;

                return new CentralApiDbContext(options);
            }
        }
    }
}