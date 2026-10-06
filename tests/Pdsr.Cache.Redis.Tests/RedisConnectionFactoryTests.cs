using System.Net;
using System.Net.Sockets;
using NSubstitute;
using Pdsr.Cache.Configurations;
using StackExchange.Redis;

namespace Pdsr.Cache.Redis.Tests;

public class RedisConnectionFactoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RedisConfiguration Config(params string[] endPoints) => new() { EndPoints = endPoints };

    #region Options (findings 4 and 5)

    [Fact]
    public void Host_names_stay_host_names()
    {
        var options = RedisConnectionFactory.CreateConfigurationOptions(Config("redis.internal:6380"));

        var endPoint = Assert.IsType<DnsEndPoint>(Assert.Single(options.EndPoints));
        Assert.Equal("redis.internal", endPoint.Host);
        Assert.Equal(6380, endPoint.Port);
    }

    [Fact]
    public void IPv6_endpoints_parse()
    {
        var options = RedisConnectionFactory.CreateConfigurationOptions(Config("[::1]:6390", "10.0.0.5:6379"));

        var ipv6 = Assert.IsType<IPEndPoint>(options.EndPoints[0]);
        Assert.Equal(AddressFamily.InterNetworkV6, ipv6.AddressFamily);
        Assert.Equal(IPAddress.IPv6Loopback, ipv6.Address);
        Assert.Equal(6390, ipv6.Port);
        Assert.Equal(new IPEndPoint(IPAddress.Parse("10.0.0.5"), 6379), options.EndPoints[1]);
    }

    [Fact]
    public void An_endpoint_without_a_port_is_accepted()
    {
        var options = RedisConnectionFactory.CreateConfigurationOptions(Config(" cache "));

        Assert.Equal("cache", Assert.IsType<DnsEndPoint>(Assert.Single(options.EndPoints)).Host);
    }

    [Fact]
    public void Settings_are_carried_into_the_options()
    {
        var options = RedisConnectionFactory.CreateConfigurationOptions(new RedisConfiguration
        {
            EndPoints = ["localhost"],
            UseSsl = true,
            AllowAdmin = true,
            AbortOnConnectFail = true,
            User = "app",
            Password = "secret",
            RetryCount = 7,
            Database = 3,
            ClientName = "orders",
        });

        Assert.True(options.Ssl);
        Assert.True(options.AllowAdmin);
        Assert.True(options.AbortOnConnectFail);
        Assert.Equal("app", options.User);
        Assert.Equal("secret", options.Password);
        Assert.Equal(7, options.ConnectRetry);
        Assert.Equal(3, options.DefaultDatabase);
        Assert.Equal("orders", options.ClientName);
    }

    [Fact]
    public void Defaults_leave_the_database_and_client_name_to_the_environment()
    {
        var withEntryName = RedisConnectionFactory.CreateConfigurationOptions(Config("localhost"));
        var withoutName = RedisConnectionFactory.CreateConfigurationOptions(new RedisConfiguration
        {
            EndPoints = ["localhost"],
            UseEntryAssemblyNameAsClientName = false,
            RetryCount = -3,
        });

        Assert.Null(withEntryName.DefaultDatabase);
        Assert.Equal(System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name, withEntryName.ClientName);
        Assert.Null(withoutName.ClientName);
        Assert.Equal(0, withoutName.ConnectRetry);
    }

    [Fact]
    public void Missing_or_blank_endpoints_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => RedisConnectionFactory.CreateConfigurationOptions(null!));
        Assert.Throws<ArgumentException>(() => RedisConnectionFactory.CreateConfigurationOptions(Config()));
        Assert.Throws<ArgumentException>(() => RedisConnectionFactory.CreateConfigurationOptions(new RedisConfiguration { EndPoints = null! }));
        Assert.Throws<ArgumentException>(() => RedisConnectionFactory.CreateConfigurationOptions(Config("localhost:6379", " ")));
        Assert.Throws<ArgumentException>(() => new RedisConnectionFactory(Config()));
    }

    [Fact]
    public void The_factory_exposes_its_options_without_connecting()
    {
        using var factory = new CountingFactory(Config("localhost:6379"));

        Assert.Single(factory.Options.EndPoints);
        Assert.Equal(0, factory.AsyncConnects);
    }

    #endregion

    #region Connection lifecycle

    [Fact]
    public async Task Async_callers_connect_asynchronously_and_share_one_connection()
    {
        using var factory = new CountingFactory(Config("localhost"));

        var first = await factory.ConnectionAsync(Ct);
        var second = await factory.ConnectionAsync(Ct);
        var third = factory.Connection();

        Assert.Same(first, second);
        Assert.Same(first, third);
        Assert.Equal(1, factory.AsyncConnects);
    }

    [Fact]
    public async Task Sync_callers_use_the_same_connect_as_async_callers()
    {
        using var factory = new CountingFactory(Config("localhost"));

        var first = factory.Connection();
        var second = await factory.ConnectionAsync(Ct);

        Assert.Same(first, second);
        Assert.Equal(1, factory.AsyncConnects);
    }

    [Fact(Timeout = 10_000)]
    public async Task A_slow_connect_does_not_block_other_callers()
    {
        // The transport blocks its calling thread until the gate opens, like an unreachable host during the connect timeout.
        using var factory = new CountingFactory(Config("localhost")) { Gate = new TaskCompletionSource(), BlockSynchronously = true };
        var syncCaller = Task.Run(factory.Connection, Ct);
        while (factory.AsyncConnects == 0) await Task.Delay(5, Ct);

        var started = DateTime.UtcNow;
        var asyncCaller = factory.ConnectionAsync(Ct);
        var elapsed = DateTime.UtcNow - started;

        Assert.False(asyncCaller.IsCompleted);
        Assert.True(elapsed < TimeSpan.FromSeconds(1), $"ConnectionAsync blocked for {elapsed}");
        factory.Gate.SetResult();
        Assert.Same(await syncCaller, await asyncCaller);
        Assert.Equal(1, factory.AsyncConnects);
    }

    [Fact]
    public async Task Concurrent_callers_wait_for_the_same_connect()
    {
        using var factory = new CountingFactory(Config("localhost")) { Gate = new TaskCompletionSource() };

        var waiting = Enumerable.Range(0, 5).Select(_ => factory.ConnectionAsync(Ct)).ToArray();
        var syncWaiter = Task.Run(factory.Connection, Ct);
        factory.Gate.SetResult();
        var connections = await Task.WhenAll(waiting);

        Assert.All(connections, c => Assert.Same(connections[0], c));
        Assert.Same(connections[0], await syncWaiter);
        Assert.Equal(1, factory.AsyncConnects);
    }

    [Fact]
    public async Task A_failed_connect_is_retried_on_the_next_call()
    {
        using var factory = new CountingFactory(Config("localhost")) { FailuresBeforeSuccess = 1 };

        await Assert.ThrowsAsync<RedisConnectionException>(() => factory.ConnectionAsync(Ct));
        var connection = await factory.ConnectionAsync(Ct);

        Assert.NotNull(connection);
        Assert.Equal(2, factory.AsyncConnects);
    }

    [Fact]
    public async Task A_connect_that_throws_synchronously_is_retried()
    {
        using var factory = new CountingFactory(Config("localhost")) { ThrowSynchronously = true, FailuresBeforeSuccess = 1 };

        await Assert.ThrowsAsync<RedisConnectionException>(() => factory.ConnectionAsync(Ct));
        Assert.NotNull(await factory.ConnectionAsync(Ct));
    }

    [Fact]
    public void A_failed_synchronous_connect_is_retried()
    {
        using var factory = new CountingFactory(Config("localhost")) { FailuresBeforeSuccess = 1 };

        Assert.Throws<RedisConnectionException>(factory.Connection);
        Assert.NotNull(factory.Connection());
        Assert.Equal(2, factory.AsyncConnects);
    }

    [Fact]
    public async Task Waiting_for_a_connect_can_be_cancelled()
    {
        using var factory = new CountingFactory(Config("localhost")) { Gate = new TaskCompletionSource() };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var waiting = factory.ConnectionAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        factory.Gate.SetResult();
        Assert.NotNull(await factory.ConnectionAsync(Ct));
    }

    [Fact]
    public async Task Dispose_closes_the_connection_and_blocks_further_use()
    {
        var factory = new CountingFactory(Config("localhost"));
        var connection = await factory.ConnectionAsync(Ct);

        factory.Dispose();
        factory.Dispose();

        connection.Received(1).Dispose();
        Assert.Throws<ObjectDisposedException>(factory.Connection);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => factory.ConnectionAsync(Ct));
    }

    [Fact]
    public async Task A_connect_in_flight_is_disposed_when_it_completes()
    {
        var factory = new CountingFactory(Config("localhost")) { Gate = new TaskCompletionSource() };
        var pending = factory.ConnectionAsync(Ct);

        factory.Dispose();
        factory.Gate.SetResult();
        var connection = await pending;

        // Disposal is a continuation on the connect, so it can run just after this test resumes.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!connection.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IDisposable.Dispose)) && DateTime.UtcNow < deadline)
            await Task.Delay(10, Ct);
        connection.Received(1).Dispose();
    }

    [Fact]
    public void Disposing_before_connecting_is_harmless()
    {
        var factory = new CountingFactory(Config("localhost"));
        factory.Dispose();
        Assert.Equal(0, factory.AsyncConnects);
    }

    private sealed class CountingFactory(IRedisConfiguration configuration) : RedisConnectionFactory(configuration)
    {
        public int AsyncConnects;

        public int FailuresBeforeSuccess { get; init; }

        public bool ThrowSynchronously { get; init; }

        public TaskCompletionSource? Gate { get; init; }

        /// <summary>Block the calling thread on <see cref="Gate"/> before returning.</summary>
        public bool BlockSynchronously { get; init; }

        protected override Task<IConnectionMultiplexer> ConnectAsync(ConfigurationOptions options)
        {
            var attempt = Interlocked.Increment(ref AsyncConnects);
            if (attempt <= FailuresBeforeSuccess && ThrowSynchronously) throw Failure();
            if (BlockSynchronously) Gate!.Task.Wait();
            return Complete(attempt);
        }

        private async Task<IConnectionMultiplexer> Complete(int attempt)
        {
            if (Gate is not null) await Gate.Task;
            await Task.Yield();
            if (attempt <= FailuresBeforeSuccess) throw Failure();
            return Substitute.For<IConnectionMultiplexer>();
        }

        private static RedisConnectionException Failure()
            => new(ConnectionFailureType.UnableToConnect, CommandFlags.None, "down", null, CommandStatus.Unknown);
    }

    #endregion
}
