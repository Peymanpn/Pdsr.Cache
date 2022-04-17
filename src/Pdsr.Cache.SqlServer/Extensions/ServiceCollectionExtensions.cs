using Pdsr.Cache;
using Pdsr.Cache.SqlServer;
using Pdsr.Cache.SqlServer.Configurations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers IDistributed cache for sql server and it with <see cref="Pdsr.Cache.ICacheManager"/>
        /// </summary>
        /// <param name="services">The Microsoft.Extensions.DependencyInjection.IServiceCollection to add services to</param>
        /// <param name="sqlServerConfiguration">sql server cache configuration.<see cref="SqlServerConfiguration"/></param>
        public static IServiceCollection AddSqlServerCacheManager(this IServiceCollection services,
            SqlServerConfiguration sqlServerConfiguration)
        {
            services.AddDistributedSqlServerCache(options =>
            {
                options.ConnectionString = sqlServerConfiguration.ConnectionString;
                options.DefaultSlidingExpiration = sqlServerConfiguration.DefaultSlidingExpiration;
                options.ExpiredItemsDeletionInterval = sqlServerConfiguration.ExpiredItemsDeletionInterval;
                options.SchemaName = sqlServerConfiguration.SchemaName;
                options.TableName = sqlServerConfiguration.TableName;
            });
            RegisterICacheManagerDependency(services);
            return services;
        }

        public static IServiceCollection AddSqlServerCacheManager(this IServiceCollection services, Action<SqlServerCacheOptions> setupAction)
        {
            services.AddDistributedSqlServerCache(setupAction);
            RegisterICacheManagerDependency(services);
            return services;
        }


        private static IServiceCollection RegisterICacheManagerDependency(IServiceCollection services)
        {
            return services.AddSingleton(f
                 => new SqlCacheManager(f.GetRequiredService<IDistributedCache>()))
                            .AddSingleton<IAsyncCacheManager>(f => f.GetRequiredService<SqlCacheManager>())
                            .AddSingleton<ISyncCacheManager>(f => f.GetRequiredService<SqlCacheManager>())
                            .AddSingleton<ICacheManager>(f => f.GetRequiredService<SqlCacheManager>())
                            ;

        }
    }
}
