using Pdsr.Cache;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {

        /// <summary>
        /// Registers <see cref="ICacheManager" /> as no <see cref="NoCacheManager" />for scenarios which redis or any other cache systems are not available/>/>
        /// </summary>
        /// <param name="services"></param>
        public static IServiceCollection AddNoCacheManager(this IServiceCollection services)
        {
            // services.AddSingleton<ICacheManager, NoCacheManager>();
            services.AddTransient(f => new NoCacheManager())
                .AddTransient<IAsyncCacheManager>(f => f.GetRequiredService<NoCacheManager>())
                .AddTransient<ISyncCacheManager>(f => f.GetRequiredService<NoCacheManager>())
                .AddSingleton<ICacheManager>(f => f.GetRequiredService<NoCacheManager>());
            ;

            return services;
        }
    }
}
