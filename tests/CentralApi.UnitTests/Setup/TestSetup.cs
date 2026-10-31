namespace CentralApi.Services.UnitTests.Setup
{
    using MapsterMapper;
    using BankSystem.Common.Mapping;

    public static class TestSetup
    {
        private static IMapper mapper;
        private static readonly object Sync = new object();
        private static bool mapperInitialized;

        public static IMapper InitializeMapper()
        {
            lock (Sync)
            {
                if (mapperInitialized)
                {
                    return mapper;
                }

                var config = MappingConfig.Create(typeof(CentralApi.Services.Models.Banks.BankBaseServiceModel).Assembly);

                mapper = new Mapper(config);
                mapperInitialized = true;

                return mapper;
            }
        }
    }
}