using Pdsr.Cache;
using Pdsr.Cache.InMemory;
using Pdsr.Cache.InMemory.Configurations;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInMemoryCacheManager(this IServiceCollection services, int? maxEnteriesCount = null)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton(f => new InMemoryCacheManager(
                f.GetRequiredService<CachedData>(),
                f.GetRequiredService<InMemoryCacheConfig>())
            )
                .AddSingleton<IAsyncCacheManager>(f => f.GetRequiredService<InMemoryCacheManager>())
                .AddSingleton<ISyncCacheManager>(f => f.GetRequiredService<InMemoryCacheManager>())
                .AddSingleton<ICacheManager>(f => f.GetRequiredService<InMemoryCacheManager>());


            //services.AddSingleton(f => new CachedData());
            services.AddSingleton<CachedData>();

            services.AddSingleton(f => new InMemoryCacheConfig { MaxEnteriesCount = maxEnteriesCount ?? 10240 });



            return services;
        }
    }
}
