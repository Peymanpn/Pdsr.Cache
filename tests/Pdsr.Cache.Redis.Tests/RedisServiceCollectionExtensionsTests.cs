using Microsoft.Extensions.DependencyInjection;
using Pdsr.Cache.Configurations;

namespace Pdsr.Cache.Redis.Tests;

public class RedisServiceCollectionExtensionsTests
{
    [Fact]
    public void Every_cache_interface_resolves_to_one_singleton_manager()
    {
        using var provider = new ServiceCollection().AddRedisCacheManager().BuildServiceProvider();

        var manager = provider.GetRequiredService<IRedisCacheManager>();
        Assert.IsType<RedisCacheManager>(manager);
        Assert.Same(manager, provider.GetRequiredService<ICacheManager>());
        Assert.Same(manager, provider.GetRequiredService<IAsyncCacheManager>());
        Assert.Same(manager, provider.GetRequiredService<ISyncCacheManager>());
        Assert.Same(provider.GetRequiredService<IRedisConnectionFactory>(), provider.GetRequiredService<IRedisConnectionFactory>());
        Assert.Equal(["localhost:6379"], provider.GetRequiredService<IRedisConfiguration>().EndPoints);
    }

    [Theory]
    [InlineData("cache.internal", 6380, "cache.internal:6380")]
    [InlineData("::1", 6379, "[::1]:6379")]
    [InlineData("[::1]", 6379, "[::1]:6379")]
    public void Host_and_port_form_a_parseable_endpoint(string host, int port, string expected)
    {
        using var provider = new ServiceCollection().AddRedisCacheManager(host, port).BuildServiceProvider();

        var configuration = provider.GetRequiredService<IRedisConfiguration>();
        Assert.Equal([expected], configuration.EndPoints);
        Assert.Single(RedisConnectionFactory.CreateConfigurationOptions(configuration).EndPoints);
    }

    [Fact]
    public void Configuration_can_come_from_a_delegate()
    {
        using var provider = new ServiceCollection()
            .AddRedisCacheManager(c => { c.EndPoints = ["a:1"]; c.KeyPrefix = "app:"; })
            .BuildServiceProvider();

        Assert.Equal("app:", provider.GetRequiredService<IRedisConfiguration>().KeyPrefix);
        Assert.NotNull(provider.GetRequiredService<ICacheManager>());
    }

    [Fact]
    public void Configuration_can_come_from_the_container()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new RedisConfiguration { EndPoints = ["b:2"] });
        services.AddRedisCacheManager(sp => sp.GetRequiredService<RedisConfiguration>());
        using var provider = services.BuildServiceProvider();

        Assert.Equal(["b:2"], provider.GetRequiredService<IRedisConfiguration>().EndPoints);
        Assert.NotNull(provider.GetRequiredService<IRedisCacheManager>());
    }

    [Fact]
    public void Registering_again_replaces_the_configuration()
    {
        using var provider = new ServiceCollection()
            .AddRedisCacheManager(new RedisConfiguration { EndPoints = ["first:1"] })
            .AddRedisCacheManager(new RedisConfiguration { EndPoints = ["second:2"] })
            .BuildServiceProvider();

        Assert.Equal(["second:2"], provider.GetRequiredService<IRedisConfiguration>().EndPoints);
    }

    [Fact]
    public void Arguments_are_validated()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddRedisCacheManager((string)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddRedisCacheManager((Action<RedisConfiguration>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddRedisCacheManager((IRedisConfiguration)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddRedisCacheManager((Func<IServiceProvider, IRedisConfiguration>)null!));
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddRedisCacheManager(new RedisConfiguration()));
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddRedisCacheManager(_ => new RedisConfiguration()));
    }
}
