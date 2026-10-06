using Microsoft.Extensions.DependencyInjection.Extensions;
using Pdsr.Cache;
using Pdsr.Cache.InMemory.Configurations;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registers <see cref="InMemoryCacheManager"/>.
    /// </summary>
    public static class InMemoryServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a process-wide in-memory cache manager.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="maxEntriesCount">Maximum number of entries; defaults to 10240</param>
        public static IServiceCollection AddInMemoryCacheManager(this IServiceCollection services, int? maxEntriesCount = null)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            var config = new InMemoryCacheConfig();
            if (maxEntriesCount is not null) config.MaxEntriesCount = maxEntriesCount.Value;

            services.TryAddSingleton(config);
            services.TryAddSingleton(sp => new InMemoryCacheManager(
                sp.GetRequiredService<InMemoryCacheConfig>(),
                sp.GetService<TimeProvider>()));
            services.TryAddSingleton<ICacheManager>(sp => sp.GetRequiredService<InMemoryCacheManager>());
            services.TryAddSingleton<IAsyncCacheManager>(sp => sp.GetRequiredService<InMemoryCacheManager>());
            services.TryAddSingleton<ISyncCacheManager>(sp => sp.GetRequiredService<InMemoryCacheManager>());
            return services;
        }
    }
}
