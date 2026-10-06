using Microsoft.Extensions.DependencyInjection.Extensions;
using Pdsr.Cache;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registers <see cref="NoCacheManager"/>.
    /// </summary>
    public static class NoCacheServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="NoCacheManager"/> as the cache manager, for environments without a cache.
        /// </summary>
        public static IServiceCollection AddNoCacheManager(this IServiceCollection services)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<NoCacheManager>();
            services.TryAddSingleton<ICacheManager>(sp => sp.GetRequiredService<NoCacheManager>());
            services.TryAddSingleton<IAsyncCacheManager>(sp => sp.GetRequiredService<NoCacheManager>());
            services.TryAddSingleton<ISyncCacheManager>(sp => sp.GetRequiredService<NoCacheManager>());
            return services;
        }
    }
}
