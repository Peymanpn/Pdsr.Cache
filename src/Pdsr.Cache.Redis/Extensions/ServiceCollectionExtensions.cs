using Pdsr.Cache.Configurations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Redis Cache Manager with minimum configurations
    /// </summary>
    /// <param name="services"></param>
    /// <param name="host"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    public static IServiceCollection AddRedisCacheManager(this IServiceCollection services, string host = "localhost", int port = 6379)
    {
        services.TryAddSingleton<IRedisConfiguration>(f => new RedisConfiguration
        {
            EndPoints = new[] { $"{host}:{port}" }
        });
        RegisterRedisCacheManagerDependencyInjection(services: services);
        return services;
    }

    public static IServiceCollection AddRedisCacheManager(this IServiceCollection services, Action<RedisConfiguration> setupRedis)
    {
        var config = new RedisConfiguration();
        setupRedis(config);
        services.AddSingleton<IRedisConfiguration>(config);
        RegisterRedisCacheManagerDependencyInjection(services);
        return services;
    }


    /// <summary>
    /// Adds Redis Cache manager with <see cref="ICacheManager"/>
    /// </summary>
    /// <param name="services">The Microsoft.Extensions.DependencyInjection.IServiceCollection to add services to</param>
    /// <param name="implementationFactory">implementation factory for RedisConfiguration</param>
    public static void AddRedisCacheManager(
        this IServiceCollection services,
        Func<IServiceProvider, IRedisConfiguration>? implementationFactory = null)
    {
        if (implementationFactory is not null)
        {
            services.TryAddSingleton<IRedisConfiguration>(implementationFactory);
        }
        RegisterRedisCacheManagerDependencyInjection(services: services);
    }

    /// <summary>
    /// Adds Redis Cache manager with <see cref="ICacheManager"/>
    /// </summary>
    /// <param name="services">The Microsoft.Extensions.DependencyInjection.IServiceCollection to add services to</param>
    /// <param name="redisConfiguration">Redis host configuration</param>
    public static IServiceCollection AddRedisCacheManager(
        this IServiceCollection services,
        IRedisConfiguration redisConfiguration
        )
    {
        services.AddSingleton<IRedisConfiguration>(redisConfiguration);
        // services.AddOptions<IRedisConfiguration>();
        RegisterRedisCacheManagerDependencyInjection(services: services);
        return services;
    }



    /// <summary>
    /// Adds Redis Cache manager with <see cref="ICacheManager"/>
    /// </summary>
    /// <param name="services">The Microsoft.Extensions.DependencyInjection.IServiceCollection to add services to</param>
    /// <param name="redisConfiguration">Redis cache configs.<see cref="IRedisConfiguration"/></param>
    public static IServiceCollection AddRedisCacheManager(
        this IServiceCollection services,
        IRedisConfiguration redisConfiguration,
        // bool registerAsDistributedCache = false,
        ServiceLifetime serviceLifetime = ServiceLifetime.Singleton,
        ServiceLifetime optionsLifetime = ServiceLifetime.Singleton
        )
    {
        RegisterRedisCacheManagerDependencyInjection(services: services, serviceLifetime: serviceLifetime, optionsLifetime: optionsLifetime);
        services.AddSingleton<IRedisConfiguration>(redisConfiguration);
        return services;
    }

    private static IServiceCollection RegisterRedisCacheManagerDependencyInjection(
        IServiceCollection services,
        ServiceLifetime serviceLifetime = ServiceLifetime.Singleton,
        ServiceLifetime optionsLifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        switch (serviceLifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IRedisCacheManager>(f
                    => new RedisCacheManager(
                        f.GetRequiredService<IRedisConfiguration>(),
                        f.GetRequiredService<IRedisConnectionFactory>())
                    )
                    .AddSingleton<IAsyncCacheManager>(f => f.GetRequiredService<IRedisCacheManager>())
                    .AddSingleton<ISyncCacheManager>(f => f.GetRequiredService<IRedisCacheManager>())
                    .AddSingleton<ICacheManager>(f => f.GetRequiredService<IRedisCacheManager>());
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped<IRedisCacheManager>(f
                    => new RedisCacheManager(
                        f.GetRequiredService<IRedisConfiguration>(),
                        f.GetRequiredService<IRedisConnectionFactory>()
                        )
                    )
                    .AddScoped<IAsyncCacheManager>(f => f.GetRequiredService<IRedisCacheManager>())
                    .AddScoped<ISyncCacheManager>(f => f.GetRequiredService<IRedisCacheManager>())
                    .AddScoped<ICacheManager>(f => f.GetRequiredService<IRedisCacheManager>());
                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IRedisCacheManager>(f
                    => new RedisCacheManager(
                        f.GetRequiredService<IRedisConfiguration>(),
                        f.GetRequiredService<IRedisConnectionFactory>()
                        )
                    )
                    .AddTransient<IAsyncCacheManager>(f => f.GetRequiredService<IRedisCacheManager>())
                    .AddTransient<ISyncCacheManager>(f => f.GetRequiredService<IRedisCacheManager>())
                    .AddTransient<ICacheManager>(f => f.GetRequiredService<IRedisCacheManager>());
                break;
            default:
                break;
        }
        switch (optionsLifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped<IRedisConnectionFactory, RedisConnectionFactory>();
                break;
            case ServiceLifetime.Transient:
                services.AddTransient<IRedisConnectionFactory, RedisConnectionFactory>();
                break;
            default:
                break;
        }
        return services;
    }
}
