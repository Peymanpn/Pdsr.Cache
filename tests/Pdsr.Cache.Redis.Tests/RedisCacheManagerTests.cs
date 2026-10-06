using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pdsr.Cache.Configurations;
using Pdsr.Cache.Tests.Shared;
using StackExchange.Redis;
using static Pdsr.Cache.Tests.Shared.AsyncSequence;

namespace Pdsr.Cache.Redis.Tests;

/// <summary>
/// Unit tests against substituted StackExchange.Redis interfaces: failure modes and exact commands sent.
/// </summary>
public class RedisCacheManagerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeRedis _redis = new();

    private IDatabase Db => _redis.Database;

    private RedisCacheManager Create(Action<RedisConfiguration>? configure = null)
    {
        var configuration = new RedisConfiguration { EndPoints = ["localhost:6379"] };
        configure?.Invoke(configuration);
        return new RedisCacheManager(configuration, _redis.Factory);
    }

    [Fact]
    public void Constructor_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new RedisCacheManager(null!, _redis.Factory));
        Assert.Throws<ArgumentNullException>(() => new RedisCacheManager(new RedisConfiguration(), null!));
    }

    #region Single read (finding 1)

    [Fact]
    public async Task TryGetAsync_sends_one_GET_and_no_existence_check()
    {
        Db.StringGetAsync("k", CommandFlags.None).Returns(FakeRedis.Json(false));
        var cache = Create();

        var result = await cache.TryGetAsync<bool>("k", Ct);

        Assert.Equal(CacheResult<bool>.Hit(false), result);
        await Db.Received(1).StringGetAsync("k", CommandFlags.None);
        await Db.DidNotReceiveWithAnyArgs().KeyExistsAsync(default(RedisKey));
        Db.DidNotReceiveWithAnyArgs().IsConnected(default);
    }

    [Fact]
    public void TryGet_sends_one_GET_and_no_existence_check()
    {
        Db.StringGet("k", CommandFlags.None).Returns(FakeRedis.Json(true));
        var cache = Create();

        Assert.True(cache.TryGet<bool>("k", out var value));
        Assert.True(value);
        Db.DidNotReceiveWithAnyArgs().KeyExists(default(RedisKey));
    }

    [Fact]
    public async Task A_key_that_expires_between_calls_is_never_read_as_a_default_value()
    {
        // The old flow was EXISTS (true) then GET (nil once expired), which produced default(bool) = false.
        Db.KeyExistsAsync("allow").Returns(true);
        Db.StringGetAsync("allow", CommandFlags.None).Returns(RedisValue.Null);
        var cache = Create();

        var result = await cache.TryGetAsync<bool>("allow", Ct);

        Assert.False(result.HasValue);
    }

    [Fact]
    public async Task Batch_get_reads_each_key_once_without_existence_checks()
    {
        Db.StringGetAsync("a", CommandFlags.None).Returns(FakeRedis.Json("cached"));
        var cache = Create();

        var results = await cache.GetAsync(
            From(Pair<Func<Task<string?>>>("a", () => Task.FromResult<string?>("fresh")),
                 Pair<Func<Task<string?>>>("b", () => Task.FromResult<string?>("fresh"))),
            null, Ct).ToListAsync(Ct);

        Assert.Equal(["cached", "fresh"], results);
        await Db.Received(1).StringGetAsync("a", CommandFlags.None);
        await Db.Received(1).StringGetAsync("b", CommandFlags.None);
        await Db.DidNotReceiveWithAnyArgs().KeyExistsAsync(default(RedisKey));
    }

    [Fact]
    public async Task A_cached_json_null_is_a_hit_but_read_through_reacquires()
    {
        Db.StringGetAsync("k", CommandFlags.None).Returns((RedisValue)"null");
        var cache = Create();

        Assert.Equal(CacheResult<string>.Hit(null), await cache.TryGetAsync<string>("k", Ct));
        Assert.Equal("fresh", await cache.GetAsync<string>("k", () => Task.FromResult<string?>("fresh"), null, Ct));
    }

    #endregion

    #region Outages (finding 2)

    [Fact]
    public async Task Async_reads_throw_when_Redis_is_unreachable()
    {
        Db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).ThrowsAsync(FakeRedis.ConnectionError());
        Db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).ThrowsAsync(FakeRedis.ConnectionError());
        var cache = Create();

        await Assert.ThrowsAsync<RedisConnectionException>(() => cache.TryGetAsync<bool>("k", Ct));
        await Assert.ThrowsAsync<RedisConnectionException>(() => cache.GetAsync<bool>("k", Ct));
        await Assert.ThrowsAsync<RedisConnectionException>(() => cache.GetAsync<bool>("k", preferReplica: true, Ct));
        await Assert.ThrowsAsync<RedisConnectionException>(() => cache.IsSetAsync("k", Ct));
    }

    [Fact]
    public void Synchronous_reads_throw_when_Redis_is_unreachable()
    {
        Db.StringGet(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Throws(FakeRedis.ConnectionError());
        Db.KeyExists(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Throws(FakeRedis.ConnectionError());
        var cache = Create();

        Assert.Throws<RedisConnectionException>(() => cache.TryGet<bool>("k", out _));
        Assert.Throws<RedisConnectionException>(() => cache.Get<bool>("k"));
        Assert.Throws<RedisConnectionException>(() => cache.Get<bool>("k", preferReplica: false));
        Assert.Throws<RedisConnectionException>(() => cache.IsSet("k"));
    }

    [Fact]
    public async Task Writes_throw_when_Redis_is_unreachable()
    {
        Db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>()).ThrowsAsync(FakeRedis.ConnectionError());
        Db.StringSet(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>()).Throws(FakeRedis.ConnectionError());
        var cache = Create();

        await Assert.ThrowsAsync<RedisConnectionException>(() => cache.SetAsync("k", 1, cancellationToken: Ct));
        Assert.Throws<RedisConnectionException>(() => cache.Set("k", 1));
    }

    [Fact]
    public async Task Read_through_falls_back_to_acquire_when_Redis_is_unreachable()
    {
        Db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).ThrowsAsync(FakeRedis.ConnectionError());
        Db.StringGet(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Throws(FakeRedis.TimeoutError());
        var cache = Create();

        Assert.True(await cache.GetAsync<bool>("allow", () => Task.FromResult(true), 60, Ct));
        Assert.True(cache.Get<bool>("allow", () => true, 60));
        await Db.DidNotReceiveWithAnyArgs().StringSetAsync(default, default, default(TimeSpan?), default(When));
    }

    [Fact]
    public async Task Read_through_returns_the_value_when_caching_it_fails()
    {
        Db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>()).ThrowsAsync(FakeRedis.TimeoutError());
        Db.StringSet(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>()).Throws(FakeRedis.ConnectionError());
        var cache = Create();

        Assert.Equal("v", await cache.GetAsync<string>("k", () => Task.FromResult<string?>("v"), 60, Ct));
        Assert.Equal("v", cache.Get<string>("k", () => "v", 60));
    }

    [Fact]
    public async Task Read_through_does_not_hide_server_errors()
    {
        Db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).ThrowsAsync(new RedisServerException(RedisErrorKind.Unknown, CommandFlags.None, "WRONGTYPE"));
        Db.StringGet(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Throws(new RedisServerException(RedisErrorKind.Unknown, CommandFlags.None, "WRONGTYPE"));
        var cache = Create();

        await Assert.ThrowsAsync<RedisServerException>(() => cache.GetAsync<string>("k", () => Task.FromResult<string?>("v"), null, Ct));
        Assert.Throws<RedisServerException>(() => cache.Get<string>("k", () => "v"));
    }

    #endregion

    #region Cancellation (finding 3)

    [Fact]
    public async Task Cancelling_stops_waiting_for_a_hung_command()
    {
        var hung = new TaskCompletionSource<RedisValue>();
        Db.StringGetAsync("k", CommandFlags.None).Returns(hung.Task);
        var cache = Create();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var read = cache.TryGetAsync<int>("k", cts.Token);
        Assert.False(read.IsCompleted);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        hung.SetException(FakeRedis.ConnectionError()); // a late failure must not surface as unobserved
    }

    [Fact]
    public async Task Cancelling_stops_waiting_for_a_hung_write()
    {
        var hung = new TaskCompletionSource<bool>();
        Db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>()).Returns(hung.Task);
        var cache = Create();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var write = cache.SetAsync("k", 1, cancellationToken: cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        hung.SetResult(true);
    }

    [Fact]
    public async Task Cancelling_stops_waiting_for_a_hung_batch_write()
    {
        var hung = new TaskCompletionSource<bool>();
        Db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>()).Returns(hung.Task);
        var cache = Create();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var batch = cache.SetAsync(From(Pair<string?>("a", "1")), null, cts.Token);
        await Task.Delay(50, Ct);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => batch);
        hung.SetException(FakeRedis.ConnectionError());
    }

    [Fact]
    public async Task Cancelling_stops_waiting_for_a_hung_flush()
    {
        var server = _redis.AddServer("10.0.0.1:6379");
        var hung = new TaskCompletionSource();
        server.FlushDatabaseAsync(Arg.Any<int>(), Arg.Any<CommandFlags>()).Returns(hung.Task);
        var cache = Create(c => c.AllowFlushDatabase = true);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var clear = cache.ClearAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clear);
        hung.SetResult();
    }

    [Fact]
    public async Task A_cancelled_token_is_rejected_before_any_command_is_sent()
    {
        var cache = Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.TryGetAsync<int>("k", cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.SetAsync("k", 1, TimeSpan.FromSeconds(1), cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.RemoveByPatternAsync("*", cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetItemTimeToLiveAsync("k", cts.Token));
        await Db.DidNotReceiveWithAnyArgs().StringGetAsync(default(RedisKey), default);
    }

    #endregion

    #region Writes (finding 6)

    [Fact]
    public void Synchronous_Set_writes_synchronously()
    {
        var cache = Create();

        cache.Set("k", 5, 30);
        cache.Set("t", 6, TimeSpan.FromMinutes(2));

        Db.Received(1).StringSet("k", FakeRedis.Json(5), TimeSpan.FromSeconds(30), When.Always);
        Db.Received(1).StringSet("t", FakeRedis.Json(6), TimeSpan.FromMinutes(2), When.Always);
        Db.DidNotReceiveWithAnyArgs().StringSetAsync(default, default, default(TimeSpan?), default(When));
    }

    [Fact]
    public async Task Batch_set_completes_only_after_every_write_has()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>()).Returns(pending.Task);
        var cache = Create();

        var batches = new[]
        {
            cache.SetAsync(From(Pair<string?>("a", "1"), Pair<string?>("b", "2")), null, Ct),
            cache.SetAsync(From(Pair("c", Task.FromResult<string?>("3"))), 60, Ct),
            cache.SetAsync(From(Pair<Func<Task<string?>>>("d", () => Task.FromResult<string?>("4"))), 60, Ct),
        };
        await Task.Delay(100, Ct);

        Assert.All(batches, b => Assert.False(b.IsCompleted));
        pending.SetResult(true);
        await Task.WhenAll(batches);
        await Db.Received(1).StringSetAsync("a", FakeRedis.Json("1"), null, When.Always);
        await Db.Received(1).StringSetAsync("c", FakeRedis.Json("3"), TimeSpan.FromSeconds(60), When.Always);
    }

    [Fact]
    public async Task Batch_set_surfaces_a_failed_write()
    {
        Db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>()).ThrowsAsync(FakeRedis.ConnectionError());
        var cache = Create();

        await Assert.ThrowsAsync<RedisConnectionException>(() => cache.SetAsync(From(Pair<string?>("a", "1")), null, Ct));
    }

    [Fact]
    public async Task Batch_set_with_a_non_positive_cache_time_writes_nothing()
    {
        var cache = Create();
        var acquired = false;

        await cache.SetAsync(From(Pair<string?>("a", "1")), 0, Ct);
        await cache.SetAsync(From(Pair("a", Task.FromResult<string?>("1"))), -1, Ct);
        await cache.SetAsync(From(Pair<Func<Task<string?>>>("a", () => { acquired = true; return Task.FromResult<string?>("1"); })), 0, Ct);

        Assert.False(acquired);
        await Db.DidNotReceiveWithAnyArgs().StringSetAsync(default, default, default(TimeSpan?), default(When));
    }

    [Fact]
    public async Task Batch_set_rejects_a_null_sequence()
    {
        var cache = Create();
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.SetAsync((IAsyncEnumerable<KeyValuePair<string, string?>>)null!, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.SetAsync((IAsyncEnumerable<KeyValuePair<string, Task<string?>>>)null!, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.SetAsync((IAsyncEnumerable<KeyValuePair<string, Func<Task<string?>>>>)null!, null, Ct));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(45)]
    public async Task Cache_time_is_sent_as_the_expiry(int? seconds)
    {
        var cache = Create();

        await cache.SetAsync("k", "v", seconds, Ct);

        TimeSpan? expected = seconds is null ? null : TimeSpan.FromSeconds(seconds.Value);
        await Db.Received(1).StringSetAsync("k", FakeRedis.Json("v"), expected, When.Always);
    }

    #endregion

    #region Reads

    [Fact]
    public async Task PreferReplica_is_passed_as_a_command_flag()
    {
        var cache = Create();

        await cache.GetAsync<int>("a", preferReplica: true, Ct);
        await cache.GetAsync<int>("b", preferReplica: false, Ct);
        cache.Get<int>("c", preferReplica: true);

        await Db.Received(1).StringGetAsync("a", CommandFlags.PreferReplica);
        await Db.Received(1).StringGetAsync("b", CommandFlags.None);
        Db.Received(1).StringGet("c", CommandFlags.PreferReplica);
    }

    [Fact]
    public async Task Key_prefix_scopes_every_key()
    {
        var cache = Create(c => c.KeyPrefix = "orders:");

        await cache.TryGetAsync<int>("1", Ct);
        await cache.SetAsync("2", 2, cancellationToken: Ct);
        cache.Remove("3");

        await Db.Received(1).StringGetAsync("orders:1", CommandFlags.None);
        await Db.Received(1).StringSetAsync("orders:2", Arg.Any<RedisValue>(), null, When.Always);
        Db.Received(1).KeyDelete("orders:3");
    }

    [Fact]
    public void The_Redis_property_is_scoped_to_the_key_prefix()
    {
        Assert.Same(Db, Create().Redis);

        var prefixed = Create(c => c.KeyPrefix = "app:").Redis;
        Assert.NotSame(Db, prefixed);
        prefixed.StringGet("x");
        Db.Received(1).StringGet("app:x", CommandFlags.None);
    }

    [Fact]
    public void The_configured_database_is_used()
    {
        _ = Create(c => c.Database = 4).Redis;
        _redis.Connection.Received().GetDatabase(4, Arg.Any<object?>());
    }

    [Fact]
    public async Task Time_to_live_is_read_from_Redis()
    {
        Db.KeyTimeToLiveAsync("k").Returns(TimeSpan.FromSeconds(42));
        Db.KeyTimeToLive("k").Returns(TimeSpan.FromSeconds(41));
        var cache = Create();

        Assert.Equal(42, await cache.GetItemTimeToLiveAsync("k", Ct));
        Assert.Equal(TimeSpan.FromSeconds(42), await cache.GetItemTimeSpanToLiveAsync("k", Ct));
        Assert.Equal(41, cache.GetItemTimeToLive("k"));
        Assert.Equal(-1, cache.GetItemTimeToLive("missing"));
        Assert.Equal(-1, await cache.GetItemTimeToLiveAsync("missing", Ct));
    }

    [Fact]
    public async Task Remove_deletes_through_the_database()
    {
        var cache = Create();

        await cache.RemoveAsync("a", Ct);
        cache.Remove("b");

        await Db.Received(1).KeyDeleteAsync("a");
        Db.Received(1).KeyDelete("b");
    }

    #endregion

    #region Patterns and clearing (finding 7)

    [Fact]
    public async Task RemoveByPattern_scans_every_primary_and_deletes_on_the_server_that_holds_the_key()
    {
        var first = _redis.AddServer("10.0.0.1:6379", keys: ["user:1"]);
        var second = _redis.AddServer("10.0.0.2:6379", keys: ["user:2"]);
        var replica = _redis.AddServer("10.0.0.3:6379", replica: true, keys: ["user:1"]);
        var cache = Create();

        await cache.RemoveByPatternAsync("user:*", Ct);

        await first.Received(1).ExecuteAsync(null, "DEL", Arg.Is<ICollection<object>>(a => a.Single().Equals((RedisKey)"user:1")), CommandFlags.None);
        await second.Received(1).ExecuteAsync(null, "DEL", Arg.Is<ICollection<object>>(a => a.Single().Equals((RedisKey)"user:2")), CommandFlags.None);
        replica.DidNotReceiveWithAnyArgs().KeysAsync();
        await first.DidNotReceive().ExecuteAsync(null, "DEL", Arg.Is<ICollection<object>>(a => a.Single().Equals((RedisKey)"user:2")), CommandFlags.None);
    }

    [Fact]
    public void Synchronous_RemoveByPattern_uses_the_same_scan()
    {
        var server = _redis.AddServer("10.0.0.1:6379", keys: ["a"]);
        var cache = Create(c => c.Database = 3);

        cache.RemoveByPattern("*");

        server.Received(1).KeysAsync(3, "*", 250, Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>());
        server.Received(1).ExecuteAsync(3, "DEL", Arg.Any<ICollection<object>>(), CommandFlags.None);
    }

    [Fact]
    public async Task RemoveByPattern_deletes_in_pipelined_pages()
    {
        var keys = Enumerable.Range(0, 600).Select(i => $"k{i}").ToArray();
        var server = _redis.AddServer("10.0.0.1:6379", keys: keys);
        var cache = Create();

        await cache.RemoveByPatternAsync("k*", Ct);

        await server.Received(600).ExecuteAsync(null, "DEL", Arg.Any<ICollection<object>>(), CommandFlags.None);
    }

    [Fact]
    public async Task RemoveByPattern_fails_rather_than_skip_a_disconnected_primary()
    {
        var up = _redis.AddServer("10.0.0.1:6379", keys: ["a"]);
        _redis.AddServer("10.0.0.2:6379", connected: false);
        var cache = Create();

        await Assert.ThrowsAsync<RedisConnectionException>(() => cache.RemoveByPatternAsync("*", Ct));
        await up.DidNotReceiveWithAnyArgs().ExecuteAsync(default(int?), default!, default!, default);
    }

    [Fact]
    public async Task RemoveByPattern_fails_when_there_is_no_primary()
    {
        _redis.AddServer("10.0.0.1:6379", replica: true);
        var cache = Create();

        var error = await Assert.ThrowsAsync<RedisConnectionException>(() => cache.RemoveByPatternAsync("*", Ct));
        Assert.Contains("No Redis primary", error.Message);
    }

    [Fact]
    public async Task RemoveByPattern_matches_the_key_prefix_literally()
    {
        var server = _redis.AddServer("10.0.0.1:6379");
        var cache = Create(c => c.KeyPrefix = @"a*b?[x]\:");

        await cache.RemoveByPatternAsync("user:*", Ct);

        server.Received(1).KeysAsync(-1, @"a\*b\?\[x\]\\:user:*", 250, Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RemoveByPattern_rejects_a_null_pattern()
    {
        var cache = Create();
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.RemoveByPatternAsync(null!, Ct));
    }

    [Fact]
    public async Task Clear_without_a_key_prefix_refuses_to_flush_by_default()
    {
        var server = _redis.AddServer("10.0.0.1:6379");
        var cache = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.ClearAsync(Ct));
        Assert.Throws<InvalidOperationException>(cache.Clear);
        await server.DidNotReceiveWithAnyArgs().FlushDatabaseAsync();
    }

    [Fact]
    public async Task Clear_flushes_every_primary_when_allowed()
    {
        var first = _redis.AddServer("10.0.0.1:6379");
        var second = _redis.AddServer("10.0.0.2:6379");
        var replica = _redis.AddServer("10.0.0.3:6379", replica: true);
        var cache = Create(c => { c.AllowFlushDatabase = true; c.Database = 2; });

        await cache.ClearAsync(Ct);

        await first.Received(1).FlushDatabaseAsync(2);
        await second.Received(1).FlushDatabaseAsync(2);
        await replica.DidNotReceiveWithAnyArgs().FlushDatabaseAsync();
    }

    [Fact]
    public async Task Clear_with_a_key_prefix_removes_only_prefixed_keys()
    {
        var server = _redis.AddServer("10.0.0.1:6379", keys: ["app:1"]);
        var cache = Create(c => { c.KeyPrefix = "app:"; c.AllowFlushDatabase = true; });

        await cache.ClearAsync(Ct);

        server.Received(1).KeysAsync(-1, "app:*", 250, Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>());
        await server.Received(1).ExecuteAsync(null, "DEL", Arg.Any<ICollection<object>>(), CommandFlags.None);
        await server.DidNotReceiveWithAnyArgs().FlushDatabaseAsync();
    }

    #endregion

    #region Keys and server access

    [Fact]
    public void Keys_come_from_every_primary_without_the_prefix()
    {
        _redis.AddServer("10.0.0.1:6379", keys: ["app:a", "app:b"]);
        _redis.AddServer("10.0.0.2:6379", keys: ["app:b", "app:c"]);
        var cache = Create(c => c.KeyPrefix = "app:");

        Assert.Equal(["a", "b", "c"], cache.Keys.Order());
    }

    [Fact]
    public void User_keys_are_matched_by_subject_and_prefix()
    {
        var server = _redis.AddServer("10.0.0.1:6379", keys: ["abcd:42:x"]);
        var cache = Create();

        Assert.Equal(["abcd:42:x"], cache.GetKeysForUser("42"));
        cache.GetKeysForUserByPrefix("42", "ab??:");

        server.Received(1).Keys(-1, "????:42:*", 250, Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>());
        server.Received(1).Keys(-1, "ab??:42:*", 250, Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public void Server_prefers_a_connected_primary()
    {
        var replica = _redis.AddServer("10.0.0.1:6379", replica: true);
        var down = _redis.AddServer("10.0.0.2:6379", connected: false);
        var primary = _redis.AddServer("10.0.0.3:6379");

        Assert.Same(primary, Create().Server);
    }

    [Fact]
    public void Server_falls_back_to_the_first_server()
    {
        var down = _redis.AddServer("10.0.0.1:6379", connected: false);

        Assert.Same(down, Create().Server);
    }

    [Fact]
    public void Subscriber_comes_from_the_connection()
    {
        var subscriber = Substitute.For<ISubscriber>();
        _redis.Connection.GetSubscriber(Arg.Any<object?>()).Returns(subscriber);

        Assert.Same(subscriber, Create().Subscriber);
    }

    [Fact]
    public void Disposing_the_manager_leaves_the_shared_connection_open()
    {
        var cache = Create();

        cache.Dispose();
        cache.Dispose();

        _redis.Connection.DidNotReceive().Dispose();
        _redis.Factory.DidNotReceive().Dispose();
    }

    #endregion

    #region Sets

    [Fact]
    public async Task Set_members_are_returned_as_strings()
    {
        Db.SetMembersAsync("s").Returns([(RedisValue)"a", (RedisValue)"b"]);
        var cache = Create();

        Assert.Equal(["a", "b"], await cache.GetSetAsync("s", Ct));
    }

    [Fact]
    public async Task Set_items_skip_members_whose_item_has_expired()
    {
        Db.SetMembersAsync("s").Returns([(RedisValue)"1", (RedisValue)"2"]);
        Db.StringGetAsync("item:1", CommandFlags.None).Returns(FakeRedis.Json("one"));
        Db.StringGetAsync("item:2", CommandFlags.None).Returns(RedisValue.Null);
        var cache = Create();

        Assert.Equal(["one"], await cache.GetSetItemsAsync<string>("s", "item:", Ct));
    }

    [Fact]
    public async Task Adding_to_a_set_caches_the_item_and_adds_the_member()
    {
        var cache = Create();

        await cache.AddToSetAsync("s", "1", "one", "item:", 60, Ct);

        await Db.Received(1).StringSetAsync("item:1", FakeRedis.Json("one"), TimeSpan.FromSeconds(60), When.Always);
        await Db.Received(1).SetAddAsync("s", "1");
    }

    [Fact]
    public async Task Removing_from_a_set_removes_the_member_and_the_item()
    {
        Db.SetLengthAsync("s").Returns(3);
        var cache = Create();

        await cache.RemoveFromSetAsync("s", "1", "item:", Ct);

        await Db.Received(1).SetRemoveAsync("s", "1");
        await Db.Received(1).KeyDeleteAsync("item:1");
        Assert.Equal(3, await cache.GetSetLengthAsync("s", Ct));
    }

    #endregion
}
