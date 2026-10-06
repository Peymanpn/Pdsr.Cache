using Pdsr.Cache.Configurations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers <see cref="RedisCacheManager"/>.
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Redis cache manager for a single server.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="host">Host name or IP address. IPv6 addresses may be given with or without brackets.</param>
    /// <param name="port">Port</param>
    public static IServiceCollection AddRedisCacheManager(this IServiceCollection services, string host = "localhost", int port = 6379)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        return services.AddRedisCacheManager(new RedisConfiguration { EndPoints = [FormatEndPoint(host, port)] });
    }

    /// <summary>
    /// Registers the Redis cache manager, configured by <paramref name="configure"/>.
    /// </summary>
    public static IServiceCollection AddRedisCacheManager(this IServiceCollection services, Action<RedisConfiguration> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var configuration = new RedisConfiguration();
        configure(configuration);
        return services.AddRedisCacheManager(configuration);
    }

    /// <summary>
    /// Registers the Redis cache manager with the given configuration.
    /// </summary>
    public static IServiceCollection AddRedisCacheManager(this IServiceCollection services, IRedisConfiguration redisConfiguration)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (redisConfiguration is null) throw new ArgumentNullException(nameof(redisConfiguration));
        services.Replace(ServiceDescriptor.Singleton(redisConfiguration));
        return RegisterServices(services);
    }

    /// <summary>
    /// Registers the Redis cache manager, resolving its configuration from the container.
    /// </summary>
    public static IServiceCollection AddRedisCacheManager(this IServiceCollection services, Func<IServiceProvider, IRedisConfiguration> configurationFactory)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configurationFactory is null) throw new ArgumentNullException(nameof(configurationFactory));
        services.Replace(ServiceDescriptor.Singleton(configurationFactory));
        return RegisterServices(services);
    }

    internal static string FormatEndPoint(string host, int port)
        => host.IndexOf(':') >= 0 && !host.StartsWith("[", StringComparison.Ordinal) ? $"[{host}]:{port}" : $"{host}:{port}";

    private static IServiceCollection RegisterServices(IServiceCollection services)
    {
        // One connection per application: the factory owns it and the container disposes it on shutdown.
        services.TryAddSingleton<IRedisConnectionFactory>(sp => new RedisConnectionFactory(sp.GetRequiredService<IRedisConfiguration>()));
        services.TryAddSingleton<IRedisCacheManager>(sp => new RedisCacheManager(
            sp.GetRequiredService<IRedisConfiguration>(),
            sp.GetRequiredService<IRedisConnectionFactory>()));
        services.TryAddSingleton<ICacheManager>(sp => sp.GetRequiredService<IRedisCacheManager>());
        services.TryAddSingleton<IAsyncCacheManager>(sp => sp.GetRequiredService<IRedisCacheManager>());
        services.TryAddSingleton<ISyncCacheManager>(sp => sp.GetRequiredService<IRedisCacheManager>());
        return services;
    }
}
