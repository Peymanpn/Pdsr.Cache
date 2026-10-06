using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Pdsr.Cache.InMemory.Configurations;

namespace Pdsr.Cache.InMemory.Tests;

public class InMemoryCacheManagerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private InMemoryCacheManager Create(int maxEntries = 100) => new(new InMemoryCacheConfig { MaxEntriesCount = maxEntries }, _time);

    #region Expiry

    [Fact]
    public async Task Entries_expire_when_their_time_is_up()
    {
        var cache = Create();
        cache.Set("k", "v", 10);

        _time.Advance(TimeSpan.FromSeconds(9));
        Assert.True(cache.TryGet<string>("k", out _));
        Assert.Equal(1, cache.GetItemTimeToLive("k"));

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cache.TryGet<string>("k", out _));
        Assert.False(cache.IsSet("k"));
        Assert.False((await cache.TryGetAsync<string>("k", Ct)).HasValue);
        Assert.Null(cache.GetItemTimeSpanToLive("k"));
    }

    [Fact]
    public void Timespan_expiry_is_honoured()
    {
        var cache = Create();
        cache.Set("k", 1, TimeSpan.FromMilliseconds(1500));

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromMilliseconds(500), cache.GetItemTimeSpanToLive("k"));

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cache.IsSet("k"));
    }

    [Fact]
    public void Reading_an_expired_entry_evicts_it()
    {
        var cache = Create();
        cache.Set("k", 1, 1);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(1, cache.Count);
        cache.TryGet<int>("k", out _);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void An_expired_entry_is_refreshed_by_read_through()
    {
        var cache = Create();
        cache.Set("k", "old", 1);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal("new", cache.Get<string>("k", () => "new", 60));
        Assert.Equal("new", cache.Get<string>("k"));
    }

    #endregion

    #region Capacity

    [Fact]
    public void A_full_cache_stops_adding_new_keys_but_still_updates_existing_ones()
    {
        var cache = Create(maxEntries: 2);
        cache.Set("a", 1);
        cache.Set("b", 2);

        cache.Set("c", 3);
        cache.Set("a", 10);

        Assert.False(cache.IsSet("c"));
        Assert.Equal(10, cache.Get<int>("a"));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Expired_entries_are_evicted_to_make_room()
    {
        var cache = Create(maxEntries: 2);
        cache.Set("short", 1, 1);
        cache.Set("long", 2);
        _time.Advance(TimeSpan.FromSeconds(2));

        cache.Set("new", 3);

        Assert.True(cache.IsSet("new"));
        Assert.True(cache.IsSet("long"));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Read_through_on_a_full_cache_still_returns_the_value()
    {
        var cache = Create(maxEntries: 1);
        cache.Set("a", 1);

        Assert.Equal(2, cache.Get<int>("b", () => 2));
        Assert.False(cache.IsSet("b"));
    }

    #endregion

    #region Patterns

    [Theory]
    [InlineData("user:*", new[] { "user:1", "user:22" })]
    [InlineData("user:?", new[] { "user:1" })]
    [InlineData("user:[12]*", new[] { "user:1", "user:22" })]
    [InlineData("user:[^1]*", new[] { "user:22" })]
    [InlineData(@"lit\*", new[] { "lit*" })]
    [InlineData("open[", new[] { "open[" })]
    [InlineData("a.b", new[] { "a.b" })]
    public void Patterns_use_glob_syntax(string pattern, string[] removed)
    {
        var cache = Create();
        string[] keys = ["user:1", "user:22", "order:1", "lit*", "litx", "open[", "a.b", "axb"];
        foreach (var key in keys) cache.Set(key, 1);

        cache.RemoveByPattern(pattern);

        Assert.Equal(keys.Except(removed).Order(), keys.Where(cache.IsSet).Order());
    }

    [Fact]
    public async Task A_null_pattern_is_rejected()
    {
        var cache = Create();
        Assert.Throws<ArgumentNullException>(() => cache.RemoveByPattern(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.RemoveByPatternAsync(null!, Ct));
    }

    #endregion

    #region Semantics

    [Fact]
    public void Reads_return_copies()
    {
        var cache = Create();
        cache.Set("list", new List<int> { 1 });

        cache.Get<List<int>>("list")!.Add(2);

        Assert.Equal([1], cache.Get<List<int>>("list"));
    }

    [Fact]
    public void Null_keys_are_rejected()
    {
        var cache = Create();
        Assert.Throws<ArgumentNullException>(() => cache.TryGet<int>(null!, out _));
        Assert.Throws<ArgumentNullException>(() => cache.Set(null!, 1));
    }

    [Fact]
    public void Constructor_requires_a_configuration()
    {
        Assert.Throws<ArgumentNullException>(() => new InMemoryCacheManager(null!));
        Assert.NotNull(new InMemoryCacheManager(new InMemoryCacheConfig()));
    }

    [Fact]
    public void Dispose_empties_the_cache()
    {
        var cache = Create();
        cache.Set("k", 1);

        cache.Dispose();

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task Async_removal_and_clearing_work()
    {
        var cache = Create();
        cache.Set("a:1", 1);
        cache.Set("b:1", 1);

        await cache.RemoveByPatternAsync("a:*", Ct);
        Assert.Equal(1, cache.Count);

        await cache.ClearAsync(Ct);
        Assert.Equal(0, cache.Count);
    }

    #endregion

    #region Dependency injection

    [Fact]
    public void Registration_shares_one_manager_and_honours_settings()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(_time);
        services.AddInMemoryCacheManager(maxEntriesCount: 1);
        using var provider = services.BuildServiceProvider();

        var manager = provider.GetRequiredService<InMemoryCacheManager>();
        Assert.Same(manager, provider.GetRequiredService<ICacheManager>());
        Assert.Same(manager, provider.GetRequiredService<IAsyncCacheManager>());
        Assert.Same(manager, provider.GetRequiredService<ISyncCacheManager>());
        Assert.Equal(1, provider.GetRequiredService<InMemoryCacheConfig>().MaxEntriesCount);

        manager.Set("k", 1, 5);
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.False(manager.IsSet("k"));
    }

    [Fact]
    public void Registration_defaults_to_the_system_clock_and_default_capacity()
    {
        using var provider = new ServiceCollection().AddInMemoryCacheManager().BuildServiceProvider();

        Assert.Equal(10240, provider.GetRequiredService<InMemoryCacheConfig>().MaxEntriesCount);
        Assert.NotNull(provider.GetRequiredService<ICacheManager>());
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddInMemoryCacheManager());
    }

    #endregion
}
