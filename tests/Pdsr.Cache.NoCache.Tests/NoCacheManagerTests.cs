using Microsoft.Extensions.DependencyInjection;
using static Pdsr.Cache.Tests.Shared.AsyncSequence;

namespace Pdsr.Cache.NoCache.Tests;

public class NoCacheManagerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly NoCacheManager _cache = new();

    [Fact]
    public async Task Nothing_is_ever_cached()
    {
        _cache.Set("k", 1);
        _cache.Set("k", 1, TimeSpan.FromMinutes(1));
        await _cache.SetAsync("k", 1, cancellationToken: Ct);
        await _cache.SetAsync("k", 1, TimeSpan.FromMinutes(1), Ct);
        await _cache.SetAsync(From(Pair<int?>("k", 1)), null, Ct);
        await _cache.SetAsync(From(Pair("k", Task.FromResult<int?>(1))), null, Ct);
        await _cache.SetAsync(From(Pair<Func<Task<int?>>>("k", () => Task.FromResult<int?>(1))), null, Ct);

        Assert.False(_cache.TryGet<int>("k", out var value));
        Assert.Equal(0, value);
        Assert.Equal(CacheResult<int>.Miss, await _cache.TryGetAsync<int>("k", Ct));
        Assert.Equal(0, _cache.Get<int>("k"));
        Assert.Equal(0, await _cache.GetAsync<int>("k", Ct));
        Assert.False(_cache.IsSet("k"));
        Assert.False(await _cache.IsSetAsync("k", Ct));
    }

    [Fact]
    public async Task Read_through_always_acquires()
    {
        var calls = 0;

        Assert.Equal(1, _cache.Get<int>("k", () => ++calls));
        Assert.Equal(2, await _cache.GetAsync<int>("k", () => Task.FromResult(++calls), null, Ct));
        Assert.Equal(3, await _cache.GetAsync<int>("k", () => ++calls, null, Ct));
        Assert.Equal(9, await _cache.GetAsync("k", Task.FromResult(9), null, Ct));
    }

    [Fact]
    public async Task Batch_reads_pass_values_through()
    {
        Assert.Equal([1], await _cache.GetAsync(From(Pair<Func<Task<int?>>>("a", () => Task.FromResult<int?>(1))), null, Ct).ToListAsync(Ct));
        Assert.Equal([2], await _cache.GetAsync(From(Pair("a", Task.FromResult<int?>(2))), null, Ct).ToListAsync(Ct));
        Assert.Equal([3], await _cache.GetAsync(From(Pair<int?>("a", 3)), null, Ct).ToListAsync(Ct));
    }

    [Fact]
    public async Task Removal_and_expiry_are_no_ops()
    {
        _cache.Remove("k");
        _cache.RemoveByPattern("*");
        _cache.Clear();
        await _cache.RemoveAsync("k", Ct);
        await _cache.RemoveByPatternAsync("*", Ct);
        await _cache.ClearAsync(Ct);

        Assert.Equal(-1, _cache.GetItemTimeToLive("k"));
        Assert.Null(_cache.GetItemTimeSpanToLive("k"));
        Assert.Equal(-1, await _cache.GetItemTimeToLiveAsync("k", Ct));
        Assert.Null(await _cache.GetItemTimeSpanToLiveAsync("k", Ct));
        _cache.Dispose();
    }

    [Fact]
    public async Task Read_through_validates_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => _cache.Get<int>("k", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.GetAsync<int>("k", (Func<Task<int>>)null!, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.GetAsync<int>("k", (Func<int>)null!, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.GetAsync<int>("k", (Task<int>)null!, null, Ct));
    }

    [Fact]
    public void Registration_shares_one_manager()
    {
        using var provider = new ServiceCollection().AddNoCacheManager().BuildServiceProvider();

        var manager = provider.GetRequiredService<NoCacheManager>();
        Assert.Same(manager, provider.GetRequiredService<ICacheManager>());
        Assert.Same(manager, provider.GetRequiredService<IAsyncCacheManager>());
        Assert.Same(manager, provider.GetRequiredService<ISyncCacheManager>());
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddNoCacheManager());
    }
}
