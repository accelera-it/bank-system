namespace BankSystem.Common.Mapping
{
    using System.Reflection;
    using Mapster;
    using MapsterMapper;
    using Microsoft.Extensions.DependencyInjection;

    public static class MappingServiceCollectionExtensions
    {
        public static IServiceCollection AddMapping(this IServiceCollection services, Assembly rootAssembly)
        {
            var config = MappingConfig.Create(rootAssembly);

            services.AddSingleton(config);
            services.AddSingleton<IMapper>(new Mapper(config));

            return services;
        }
    }
}
