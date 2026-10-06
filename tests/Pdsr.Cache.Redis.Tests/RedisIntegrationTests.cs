using Pdsr.Cache.Configurations;
using StackExchange.Redis;

namespace Pdsr.Cache.Redis.Tests;

/// <summary>
/// Redis-specific behaviour against real servers.
/// </summary>
public class RedisIntegrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly RedisFixture _redis;

    public RedisIntegrationTests(RedisFixture redis)
    {
        redis.RequireDocker();
        _redis = redis;
    }

    private IDatabase Raw => _redis.Factory.Connection().GetDatabase();

    [Fact]
    public async Task A_value_expires_after_its_time_to_live()
    {
        var cache = _redis.CreateCache();
        await cache.SetAsync("short", "v", TimeSpan.FromMilliseconds(300), Ct);
        Assert.True((await cache.TryGetAsync<string>("short", Ct)).HasValue);

        await Task.Delay(800, Ct);

        Assert.False((await cache.TryGetAsync<string>("short", Ct)).HasValue);
    }

    [Fact]
    public async Task Key_prefixes_isolate_caches_sharing_a_server()
    {
        var orders = _redis.CreateCache();
        var users = _redis.CreateCache();
        await orders.SetAsync("1", "order", cancellationToken: Ct);
        await users.SetAsync("1", "user", cancellationToken: Ct);

        Assert.Equal("order", await orders.GetAsync<string>("1", Ct));
        Assert.Equal("user", await users.GetAsync<string>("1", Ct));

        await orders.ClearAsync(Ct);
        await orders.RemoveByPatternAsync("*", Ct);

        Assert.False(await orders.IsSetAsync("1", Ct));
        Assert.Equal("user", await users.GetAsync<string>("1", Ct));
    }

    [Fact]
    public async Task Keys_are_stored_under_the_prefix_and_listed_without_it()
    {
        var prefix = $"test:{Guid.NewGuid():N}:";
        var cache = _redis.CreateCache(prefix);
        await cache.SetAsync("abcd:42:profile", 1, cancellationToken: Ct);
        await cache.SetAsync("other", 2, cancellationToken: Ct);

        Assert.True(await Raw.KeyExistsAsync(prefix + "other"));
        Assert.Equal(["abcd:42:profile", "other"], cache.Keys.Order());
        Assert.Equal(["abcd:42:profile"], cache.GetKeysForUser("42"));
        Assert.Empty(cache.GetKeysForUserByPrefix("42", "zz??:"));
    }

    [Fact]
    public async Task A_prefix_with_glob_characters_is_matched_literally()
    {
        var id = Guid.NewGuid().ToString("N");
        var star = _redis.CreateCache($"{id}*:");
        var plain = _redis.CreateCache($"{id}x:");
        await star.SetAsync("k", 1, cancellationToken: Ct);
        await plain.SetAsync("k", 2, cancellationToken: Ct);

        await star.ClearAsync(Ct);

        Assert.False(await star.IsSetAsync("k", Ct));
        Assert.True(await plain.IsSetAsync("k", Ct));
    }

    [Fact]
    public async Task Clear_without_a_prefix_is_refused_and_deletes_nothing()
    {
        var key = $"unprefixed:{Guid.NewGuid():N}";
        await Raw.StringSetAsync(key, "keep");
        var cache = new RedisCacheManager(_redis.Configuration(), _redis.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.ClearAsync(Ct));

        Assert.True(await Raw.KeyExistsAsync(key));
        await Raw.KeyDeleteAsync(key);
    }

    [Fact]
    public async Task Clear_flushes_only_the_configured_database_when_allowed()
    {
        var configuration = _redis.Configuration(c => { c.Database = 15; c.AllowAdmin = true; c.AllowFlushDatabase = true; });
        using var factory = new RedisConnectionFactory(configuration);
        var cache = new RedisCacheManager(configuration, factory);
        var keepKey = $"db0:{Guid.NewGuid():N}";
        await Raw.StringSetAsync(keepKey, "keep");
        await cache.SetAsync("doomed", 1, cancellationToken: Ct);

        await cache.ClearAsync(Ct);

        Assert.False(await cache.IsSetAsync("doomed", Ct));
        Assert.True(await Raw.KeyExistsAsync(keepKey));
        await Raw.KeyDeleteAsync(keepKey);
    }

    [Fact]
    public async Task Pattern_removal_and_key_listing_cover_every_primary()
    {
        var id = Guid.NewGuid().ToString("N");
        var configuration = _redis.Configuration(c => c.EndPoints = [_redis.PrimaryEndPoint, _redis.SecondaryEndPoint]);
        using var both = new RedisConnectionFactory(configuration);
        using var secondaryOnly = new RedisConnectionFactory(_redis.Configuration(c => c.EndPoints = [_redis.SecondaryEndPoint]));
        await Raw.StringSetAsync($"{id}:on-primary", "1");
        await secondaryOnly.Connection().GetDatabase().StringSetAsync($"{id}:on-secondary", "2");
        var cache = new RedisCacheManager(configuration, both);

        Assert.Equal([$"{id}:on-primary", $"{id}:on-secondary"], cache.Keys.Where(k => k.StartsWith(id)).Order());

        await cache.RemoveByPatternAsync($"{id}:*", Ct);

        Assert.False(await Raw.KeyExistsAsync($"{id}:on-primary"));
        Assert.False(await secondaryOnly.Connection().GetDatabase().KeyExistsAsync($"{id}:on-secondary"));
    }

    [Fact]
    public async Task Synchronous_pattern_removal_deletes_matching_keys()
    {
        var cache = _redis.CreateCache();
        cache.Set("a:1", 1);
        cache.Set("b:1", 1);

        cache.RemoveByPattern("a:*");

        Assert.False(cache.IsSet("a:1"));
        Assert.True(cache.IsSet("b:1"));
    }

    [Fact]
    public async Task A_host_name_endpoint_connects()
    {
        var port = _redis.PrimaryEndPoint.Split(':').Last();
        var configuration = _redis.Configuration(c => { c.EndPoints = [$"localhost:{port}"]; c.KeyPrefix = $"test:{Guid.NewGuid():N}:"; });
        using var factory = new RedisConnectionFactory(configuration);
        var cache = new RedisCacheManager(configuration, factory);

        await cache.SetAsync("k", 1, cancellationToken: Ct);

        Assert.Equal(1, await cache.GetAsync<int>("k", Ct));
    }

    [Fact]
    public async Task Replica_preferring_reads_fall_back_to_the_primary()
    {
        var cache = _redis.CreateCache();
        await cache.SetAsync("k", "v", cancellationToken: Ct);

        Assert.Equal("v", await cache.GetAsync<string>("k", preferReplica: true, Ct));
        Assert.Equal("v", cache.Get<string>("k", preferReplica: true));
    }

    [Fact]
    public async Task Sets_track_their_items()
    {
        var cache = _redis.CreateCache();
        await cache.AddToSetAsync("cart", "1", "apple", "item:", cancellationToken: Ct);
        await cache.AddToSetAsync("cart", "2", "pear", "item:", 1, Ct);
        await cache.AddToSetAsync("cart", "3", "plum", "item:", cancellationToken: Ct);

        Assert.Equal(3, await cache.GetSetLengthAsync("cart", Ct));
        Assert.Equal(["1", "2", "3"], (await cache.GetSetAsync("cart", Ct)).Order());

        await cache.RemoveFromSetAsync("cart", "3", "item:", Ct);
        await cache.RemoveAsync("item:2", Ct);

        Assert.Equal(["apple"], await cache.GetSetItemsAsync<string>("cart", "item:", Ct));
        Assert.False(await cache.IsSetAsync("item:3", Ct));
    }

    [Fact]
    public async Task Pub_sub_and_server_access_work()
    {
        var cache = _redis.CreateCache();
        var channel = RedisChannel.Literal($"test:{Guid.NewGuid():N}");
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await cache.Subscriber.SubscribeAsync(channel, (_, message) => received.TrySetResult(message!));

        await cache.Subscriber.PublishAsync(channel, "hello");

        Assert.Equal("hello", await received.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.True(cache.Server.IsConnected);
    }

    [Fact]
    public async Task An_unreachable_server_raises_an_error_instead_of_a_miss()
    {
        var configuration = new RedisConfiguration { EndPoints = ["127.0.0.1:1"], AbortOnConnectFail = false, RetryCount = 0 };
        var options = RedisConnectionFactory.CreateConfigurationOptions(configuration);
        options.ConnectTimeout = 500;
        options.SyncTimeout = 500;
        options.AsyncTimeout = 500;
        using var factory = new OptionsFactory(configuration, options);
        var cache = new RedisCacheManager(configuration, factory);

        await Assert.ThrowsAnyAsync<RedisException>(() => cache.TryGetAsync<bool>("allow", Ct));
        Assert.True(await cache.GetAsync<bool>("allow", () => Task.FromResult(true), 60, Ct));
    }

    /// <summary>Uses pre-built options so the test can shorten timeouts.</summary>
    private sealed class OptionsFactory(IRedisConfiguration configuration, ConfigurationOptions options) : RedisConnectionFactory(configuration)
    {
        protected override Task<IConnectionMultiplexer> ConnectAsync(ConfigurationOptions _) => base.ConnectAsync(options);

        protected override IConnectionMultiplexer Connect(ConfigurationOptions _) => base.Connect(options);
    }
}
