namespace BankSystem.Common.Mapping
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Interfaces;
    using Mapster;

    public static class MappingConfig
    {
        private static readonly string[] ScannedAssemblyPrefixes = { "BankSystem", "CentralApi" };

        /// <summary>
        /// Builds the mapping configuration from <see cref="IMapWith{TModel}"/> and <see cref="IHaveCustomMapping"/>
        /// implementations found in <paramref name="rootAssembly"/> and the project assemblies it references.
        /// </summary>
        public static TypeAdapterConfig Create(Assembly rootAssembly)
        {
            var config = new TypeAdapterConfig();
            var types = LoadAssemblies(rootAssembly)
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract)
                .ToArray();

            // Bidirectional mapping for all types implementing IMapWith<TModel>
            foreach (var type in types)
            {
                var mappedTypes = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapWith<>))
                    .Select(i => i.GetGenericArguments()[0]);

                foreach (var mappedType in mappedTypes)
                {
                    config.ForType(type, mappedType);
                    config.ForType(mappedType, type);
                }
            }

            // Custom mappings are applied last so they are not overridden
            foreach (var type in types.Where(t => typeof(IHaveCustomMapping).IsAssignableFrom(t)))
            {
                ((IHaveCustomMapping)Activator.CreateInstance(type)).ConfigureMapping(config);
            }

            return config;
        }

        // Assemblies are loaded lazily, so walk the references explicitly instead of relying on the AppDomain
        private static IEnumerable<Assembly> LoadAssemblies(Assembly rootAssembly)
        {
            var loaded = new Dictionary<string, Assembly> { [rootAssembly.FullName] = rootAssembly };
            var pending = new Queue<Assembly>(new[] { rootAssembly });

            while (pending.Count > 0)
            {
                foreach (var reference in pending.Dequeue().GetReferencedAssemblies())
                {
                    if (loaded.ContainsKey(reference.FullName)
                        || !ScannedAssemblyPrefixes.Any(p => reference.Name.StartsWith(p, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    var assembly = Assembly.Load(reference);
                    loaded[reference.FullName] = assembly;
                    pending.Enqueue(assembly);
                }
            }

            return loaded.Values;
        }
    }
}
