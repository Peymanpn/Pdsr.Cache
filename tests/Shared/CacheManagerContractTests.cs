using static Pdsr.Cache.Tests.Shared.AsyncSequence;

namespace Pdsr.Cache.Tests.Shared;

/// <summary>
/// Behaviour every <see cref="ICacheManager"/> must share. Each provider's test class derives from this.
/// Keys are unique per test, so one cache instance can serve a whole class.
/// </summary>
public abstract class CacheManagerContractTests
{
    protected abstract ICacheManager Cache { get; }

    /// <summary>False for stores that can't report expiry.</summary>
    protected virtual bool SupportsTimeToLive => true;

    /// <summary>False for stores that can't enumerate keys.</summary>
    protected virtual bool SupportsKeyEnumeration => true;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static string NewKey(string name = "k") => $"{name}:{Guid.NewGuid():N}";

    private static readonly Sample Value = new(7, "seven", true);

    #region Single reads

    [Fact]
    public async Task TryGet_on_a_missing_key_is_a_miss()
    {
        var key = NewKey();

        Assert.False(Cache.TryGet<Sample>(key, out var value));
        Assert.Null(value);
        Assert.Equal(CacheResult<Sample>.Miss, await Cache.TryGetAsync<Sample>(key, Ct));
    }

    [Fact]
    public async Task TryGet_returns_a_stored_value()
    {
        var key = NewKey();
        await Cache.SetAsync(key, Value, cancellationToken: Ct);

        Assert.True(Cache.TryGet<Sample>(key, out var value));
        Assert.Equal(Value, value);
        Assert.Equal(CacheResult<Sample>.Hit(Value), await Cache.TryGetAsync<Sample>(key, Ct));
    }

    [Fact]
    public async Task A_cached_false_is_a_hit_not_a_miss()
    {
        var key = NewKey("allow");
        await Cache.SetAsync(key, false, cancellationToken: Ct);

        var result = await Cache.TryGetAsync<bool>(key, Ct);

        Assert.True(result.HasValue);
        Assert.False(result.Value);
        Assert.True(Cache.TryGet<bool>(key, out var value));
        Assert.False(value);
    }

    [Fact]
    public async Task Get_returns_default_on_a_miss_and_the_value_on_a_hit()
    {
        var key = NewKey();
        Assert.Null(Cache.Get<Sample>(key));
        Assert.Null(await Cache.GetAsync<Sample>(key, Ct));

        Cache.Set(key, Value);

        Assert.Equal(Value, Cache.Get<Sample>(key));
        Assert.Equal(Value, await Cache.GetAsync<Sample>(key, Ct));
    }

    [Fact]
    public async Task IsSet_reports_presence()
    {
        var key = NewKey();
        Assert.False(Cache.IsSet(key));
        Assert.False(await Cache.IsSetAsync(key, Ct));

        Cache.Set(key, 1);

        Assert.True(Cache.IsSet(key));
        Assert.True(await Cache.IsSetAsync(key, Ct));
    }

    #endregion

    #region Writes

    [Fact]
    public async Task Set_overwrites_an_existing_value()
    {
        var key = NewKey();
        Cache.Set(key, "first");
        Cache.Set(key, "second");
        Assert.Equal("second", Cache.Get<string>(key));

        await Cache.SetAsync(key, "third", cancellationToken: Ct);
        Assert.Equal("third", await Cache.GetAsync<string>(key, Ct));
    }

    [Fact]
    public async Task Null_values_are_not_cached()
    {
        var key = NewKey();
        Cache.Set<Sample>(key, null);
        Cache.Set<Sample>(key, null, TimeSpan.FromMinutes(1));
        await Cache.SetAsync<Sample>(key, null, cancellationToken: Ct);
        await Cache.SetAsync<Sample>(key, null, TimeSpan.FromMinutes(1), Ct);

        Assert.False(await Cache.IsSetAsync(key, Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_non_positive_cache_time_does_not_cache(int seconds)
    {
        var key = NewKey();
        Cache.Set(key, Value, seconds);
        Cache.Set(key, Value, TimeSpan.FromSeconds(seconds));
        await Cache.SetAsync(key, Value, seconds, Ct);
        await Cache.SetAsync(key, Value, TimeSpan.FromSeconds(seconds), Ct);

        Assert.False(await Cache.IsSetAsync(key, Ct));
    }

    [Fact]
    public async Task Values_round_trip_for_common_types()
    {
        var bytesKey = NewKey();
        await Cache.SetAsync(bytesKey, new byte[] { 1, 2, 3 }, cancellationToken: Ct);
        Assert.Equal(new byte[] { 1, 2, 3 }, await Cache.GetAsync<byte[]>(bytesKey, Ct));

        var listKey = NewKey();
        await Cache.SetAsync(listKey, new List<int> { 3, 1, 2 }, cancellationToken: Ct);
        Assert.Equal([3, 1, 2], await Cache.GetAsync<List<int>>(listKey, Ct));

        var longKey = NewKey();
        Cache.Set(longKey, long.MaxValue, TimeSpan.FromMinutes(1));
        Assert.Equal(long.MaxValue, Cache.Get<long>(longKey));
    }

    [Fact]
    public async Task Remove_deletes_a_key()
    {
        var first = NewKey();
        var second = NewKey();
        Cache.Set(first, 1);
        Cache.Set(second, 2);

        Cache.Remove(first);
        await Cache.RemoveAsync(second, Ct);

        Assert.False(Cache.IsSet(first));
        Assert.False(Cache.IsSet(second));
    }

    #endregion

    #region Read-through

    [Fact]
    public async Task GetAsync_with_acquire_calls_acquire_once_and_then_serves_the_cache()
    {
        var key = NewKey();
        var calls = 0;
        Task<Sample?> Acquire() { calls++; return Task.FromResult<Sample?>(Value); }

        Assert.Equal(Value, await Cache.GetAsync<Sample>(key, Acquire, 60, Ct));
        Assert.Equal(Value, await Cache.GetAsync<Sample>(key, Acquire, 60, Ct));

        Assert.Equal(1, calls);
        Assert.Equal(Value, await Cache.GetAsync<Sample>(key, Ct));
    }

    [Fact]
    public async Task Read_through_with_a_null_result_returns_null_and_does_not_cache()
    {
        var key = NewKey();
        var calls = 0;

        Assert.Null(await Cache.GetAsync<Sample>(key, () => { calls++; return Task.FromResult<Sample?>(null); }, null, Ct));
        Assert.Null(await Cache.GetAsync<Sample>(key, () => { calls++; return (Sample?)null; }, null, Ct));
        Assert.Null(Cache.Get<Sample>(key, () => { calls++; return null; }));

        Assert.Equal(3, calls);
        Assert.False(await Cache.IsSetAsync(key, Ct));
    }

    [Fact]
    public async Task Synchronous_acquire_is_not_invoked_on_a_hit()
    {
        var key = NewKey();
        await Cache.SetAsync(key, Value, cancellationToken: Ct);

        var result = await Cache.GetAsync<Sample>(key, (Func<Sample?>)(() => throw new InvalidOperationException("acquire ran on a hit")), null, Ct);

        Assert.Equal(Value, result);
    }

    [Fact]
    public async Task GetAsync_with_a_task_caches_its_result()
    {
        var key = NewKey();

        Assert.Equal(Value, await Cache.GetAsync(key, Task.FromResult<Sample?>(Value), 60, Ct));
        Assert.Equal(Value, await Cache.GetAsync<Sample>(key, Ct));
    }

    [Fact]
    public void Synchronous_read_through_calls_acquire_once()
    {
        var key = NewKey();
        var calls = 0;
        Sample? Acquire() { calls++; return Value; }

        Assert.Equal(Value, Cache.Get<Sample>(key, Acquire, 60));
        Assert.Equal(Value, Cache.Get<Sample>(key, Acquire, 60));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Read_through_validates_arguments()
    {
        var key = NewKey();
        await Assert.ThrowsAsync<ArgumentNullException>(() => Cache.GetAsync<Sample>(key, (Func<Task<Sample?>>)null!, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Cache.GetAsync<Sample>(key, (Func<Sample?>)null!, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Cache.GetAsync<Sample>(key, (Task<Sample?>)null!, null, Ct));
        Assert.Throws<ArgumentNullException>(() => Cache.Get<Sample>(key, null!, null));
    }

    #endregion

    #region Batches

    [Fact]
    public async Task Batch_get_with_delegates_only_acquires_misses_and_keeps_order()
    {
        var cached = NewKey();
        var missing = NewKey();
        await Cache.SetAsync(cached, "cached", cancellationToken: Ct);
        var acquired = new List<string>();

        var results = await Cache.GetAsync(
            From(
                Pair<Func<Task<string?>>>(cached, () => { acquired.Add(cached); return Task.FromResult<string?>("fresh"); }),
                Pair<Func<Task<string?>>>(missing, () => { acquired.Add(missing); return Task.FromResult<string?>("fresh"); })),
            60, Ct).ToListAsync(Ct);

        Assert.Equal(["cached", "fresh"], results);
        Assert.Equal([missing], acquired);
        Assert.Equal("fresh", await Cache.GetAsync<string>(missing, Ct));
    }

    [Fact]
    public async Task Batch_get_with_tasks_fills_misses()
    {
        var cached = NewKey();
        var missing = NewKey();
        await Cache.SetAsync(cached, 1, cancellationToken: Ct);

        var results = await Cache.GetAsync(
            From(Pair(cached, Task.FromResult<int?>(100)), Pair(missing, Task.FromResult<int?>(2))),
            null, Ct).ToListAsync(Ct);

        Assert.Equal([1, 2], results);
        Assert.Equal(2, await Cache.GetAsync<int?>(missing, Ct));
    }

    [Fact]
    public async Task Batch_get_with_values_fills_misses()
    {
        var cached = NewKey();
        var missing = NewKey();
        await Cache.SetAsync(cached, "old", cancellationToken: Ct);

        var results = await Cache.GetAsync(From(Pair<string?>(cached, "new"), Pair<string?>(missing, "new")), null, Ct).ToListAsync(Ct);

        Assert.Equal(["old", "new"], results);
        Assert.Equal("new", await Cache.GetAsync<string>(missing, Ct));
    }

    [Fact]
    public async Task Batch_set_with_delegates_writes_every_non_null_value_before_completing()
    {
        var a = NewKey();
        var b = NewKey();
        var nothing = NewKey();

        await Cache.SetAsync(From(
            Pair<Func<Task<string?>>>(a, () => Task.FromResult<string?>("a")),
            Pair<Func<Task<string?>>>(b, async () => { await Task.Delay(20, Ct); return "b"; }),
            Pair<Func<Task<string?>>>(nothing, () => Task.FromResult<string?>(null))), 60, Ct);

        Assert.Equal("a", await Cache.GetAsync<string>(a, Ct));
        Assert.Equal("b", await Cache.GetAsync<string>(b, Ct));
        Assert.False(await Cache.IsSetAsync(nothing, Ct));
    }

    [Fact]
    public async Task Batch_set_with_tasks_writes_every_non_null_value()
    {
        var a = NewKey();
        var nothing = NewKey();

        await Cache.SetAsync(From(Pair(a, Task.FromResult<string?>("a")), Pair(nothing, Task.FromResult<string?>(null))), null, Ct);

        Assert.Equal("a", await Cache.GetAsync<string>(a, Ct));
        Assert.False(await Cache.IsSetAsync(nothing, Ct));
    }

    [Fact]
    public async Task Batch_set_with_values_writes_every_non_null_value()
    {
        var a = NewKey();
        var nothing = NewKey();

        await Cache.SetAsync(From(Pair<string?>(a, "a"), Pair<string?>(nothing, null)), 60, Ct);

        Assert.Equal("a", await Cache.GetAsync<string>(a, Ct));
        Assert.False(await Cache.IsSetAsync(nothing, Ct));
    }

    #endregion

    #region Time to live

    [Fact]
    public async Task Time_to_live_reflects_the_expiry()
    {
        Assert.SkipUnless(SupportsTimeToLive, "This store doesn't expose expiry.");
        var key = NewKey();
        await Cache.SetAsync(key, Value, 100, Ct);

        var ttl = Cache.GetItemTimeSpanToLive(key);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(100));
        Assert.InRange(Cache.GetItemTimeToLive(key), 90, 100);
        Assert.InRange((await Cache.GetItemTimeSpanToLiveAsync(key, Ct))!.Value, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(100));
        Assert.InRange(await Cache.GetItemTimeToLiveAsync(key, Ct), 90, 100);
    }

    [Fact]
    public async Task Time_to_live_is_minus_one_or_null_without_expiry_or_key()
    {
        var persistent = NewKey();
        var missing = NewKey();
        await Cache.SetAsync(persistent, Value, cancellationToken: Ct);

        foreach (var key in new[] { persistent, missing })
        {
            Assert.Null(Cache.GetItemTimeSpanToLive(key));
            Assert.Equal(-1, Cache.GetItemTimeToLive(key));
            Assert.Null(await Cache.GetItemTimeSpanToLiveAsync(key, Ct));
            Assert.Equal(-1, await Cache.GetItemTimeToLiveAsync(key, Ct));
        }
    }

    #endregion

    #region Patterns and clearing

    [Fact]
    public async Task RemoveByPattern_removes_only_matching_keys()
    {
        Assert.SkipUnless(SupportsKeyEnumeration, "This store can't enumerate keys.");
        var scope = Guid.NewGuid().ToString("N");
        Cache.Set($"{scope}:user:1", 1);
        Cache.Set($"{scope}:user:2", 2);
        Cache.Set($"{scope}:order:1", 3);

        await Cache.RemoveByPatternAsync($"{scope}:user:*", Ct);

        Assert.False(Cache.IsSet($"{scope}:user:1"));
        Assert.False(Cache.IsSet($"{scope}:user:2"));
        Assert.True(Cache.IsSet($"{scope}:order:1"));

        Cache.RemoveByPattern($"{scope}:order:?");
        Assert.False(Cache.IsSet($"{scope}:order:1"));
    }

    [Fact]
    public async Task RemoveByPattern_supports_character_classes()
    {
        Assert.SkipUnless(SupportsKeyEnumeration, "This store can't enumerate keys.");
        var scope = Guid.NewGuid().ToString("N");
        foreach (var suffix in new[] { "a", "b", "c" }) Cache.Set($"{scope}:{suffix}", suffix);

        await Cache.RemoveByPatternAsync($"{scope}:[ab]", Ct);

        Assert.False(Cache.IsSet($"{scope}:a"));
        Assert.False(Cache.IsSet($"{scope}:b"));
        Assert.True(Cache.IsSet($"{scope}:c"));
    }

    [Fact]
    public async Task Clear_removes_this_caches_keys()
    {
        Assert.SkipUnless(SupportsKeyEnumeration, "This store can't enumerate keys.");
        var first = NewKey();
        var second = NewKey();
        Cache.Set(first, 1);
        await Cache.ClearAsync(Ct);
        Assert.False(Cache.IsSet(first));

        Cache.Set(second, 2);
        Cache.Clear();
        Assert.False(Cache.IsSet(second));
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task A_cancelled_token_stops_async_operations()
    {
        var key = NewKey();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache.TryGetAsync<Sample>(key, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache.GetAsync<Sample>(key, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache.SetAsync(key, Value, cancellationToken: cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache.IsSetAsync(key, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache.RemoveAsync(key, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache.GetAsync<Sample>(key, () => Task.FromResult<Sample?>(Value), null, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache.SetAsync(From(Pair<string?>(key, "x")), null, cancelled.Token));
    }

    #endregion
}
