using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Pdsr.Cache.Configurations;
using StackExchange.Redis;

namespace Pdsr.Cache.Redis.Tests;

/// <summary>
/// Behaviour against a server that refuses connections. Needs no Docker: the endpoint is a local port that was just freed.
/// </summary>
public sealed class RedisOutageTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly List<RedisConnectionFactory> _factories = [];

    /// <summary>
    /// A cache whose connection has already failed, so timings cover the command alone, not the initial connect.
    /// </summary>
    private async Task<RedisCacheManager> DisconnectedCache(Action<RedisConfiguration> configure)
    {
        var configuration = new RedisConfiguration
        {
            EndPoints = [$"127.0.0.1:{ClosedPort()}"],
            AbortOnConnectFail = false,
            RetryCount = 0,
        };
        configure(configuration);
        var factory = new RedisConnectionFactory(configuration);
        _factories.Add(factory);

        var connection = await factory.ConnectionAsync(Ct);
        Assert.False(connection.IsConnected);
        return new RedisCacheManager(configuration, factory);
    }

    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<(Exception Error, TimeSpan Elapsed)> Fails(Func<Task> call)
    {
        var watch = Stopwatch.StartNew();
        var error = await Assert.ThrowsAnyAsync<RedisException>(call);
        return (error, watch.Elapsed);
    }

    [Fact]
    public async Task Fail_fast_throws_a_connection_error_immediately()
    {
        var cache = await DisconnectedCache(c => c.FailFastWhenDisconnected = true);

        var read = await Fails(() => cache.TryGetAsync<bool>("allow", Ct));
        var write = await Fails(() => cache.SetAsync("allow", true, cancellationToken: Ct));
        var watch = Stopwatch.StartNew();
        Assert.Throws<RedisConnectionException>(() => cache.TryGet<bool>("allow", out _));
        var syncRead = watch.Elapsed;

        Assert.IsType<RedisConnectionException>(read.Error);
        Assert.IsType<RedisConnectionException>(write.Error);
        Assert.True(read.Elapsed < TimeSpan.FromSeconds(1), $"read took {read.Elapsed}");
        Assert.True(write.Elapsed < TimeSpan.FromSeconds(1), $"write took {write.Elapsed}");
        Assert.True(syncRead < TimeSpan.FromSeconds(1), $"sync read took {syncRead}");
    }

    [Fact]
    public async Task Fail_fast_still_lets_read_through_fall_back_to_acquire()
    {
        var cache = await DisconnectedCache(c => c.FailFastWhenDisconnected = true);
        var watch = Stopwatch.StartNew();

        Assert.True(await cache.GetAsync<bool>("allow", () => Task.FromResult(true), 60, Ct));
        Assert.Equal("fresh", cache.Get<string>("name", () => "fresh", 60));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"read-through took {watch.Elapsed}");
    }

    [Fact]
    public async Task Without_fail_fast_a_command_waits_for_the_configured_timeout()
    {
        var cache = await DisconnectedCache(c => { c.AsyncTimeout = 1500; c.SyncTimeout = 1500; });

        var read = await Fails(() => cache.TryGetAsync<bool>("allow", Ct));

        Assert.InRange(read.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
        Assert.True(read.Error is RedisConnectionException or RedisTimeoutException, read.Error.GetType().Name);
    }

    [Fact]
    public async Task Without_fail_fast_read_through_falls_back_after_the_timeout()
    {
        var cache = await DisconnectedCache(c => c.AsyncTimeout = 1000);

        Assert.True(await cache.GetAsync<bool>("allow", () => Task.FromResult(true), 60, Ct));
    }

    public void Dispose()
    {
        foreach (var factory in _factories) factory.Dispose();
    }
}
