using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pdsr.Cache.SqlServer.Configurations;
using static Pdsr.Cache.Tests.Shared.AsyncSequence;

namespace Pdsr.Cache.SqlServer.Tests;

public class SqlCacheManagerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IDistributedCache _store = Substitute.For<IDistributedCache>();

    public SqlCacheManagerTests()
    {
        // A missing key reads as null; NSubstitute would otherwise return an empty array.
        _store.Get(Arg.Any<string>()).Returns((byte[]?)null);
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
    }

    private SqlCacheManager Create() => new(_store);

    [Fact]
    public void Constructor_requires_a_store()
        => Assert.Throws<ArgumentNullException>(() => new SqlCacheManager(null!));

    [Theory]
    [InlineData(null)]
    [InlineData(30)]
    public async Task Cache_time_becomes_an_absolute_expiry(int? seconds)
    {
        var cache = Create();
        TimeSpan? expected = seconds is null ? null : TimeSpan.FromSeconds(seconds.Value);

        await cache.SetAsync("async", 1, seconds, Ct);
        cache.Set("sync", 1, seconds);

        await _store.Received(1).SetAsync("async", Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == expected), Arg.Any<CancellationToken>());
        _store.Received(1).Set("sync", Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == expected));
    }

    [Fact]
    public void Synchronous_read_through_caches_with_the_given_expiry()
    {
        var cache = Create();

        cache.Get<int>("k", () => 5, 45);

        _store.Received(1).Set("k", Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(45)));
    }

    [Fact]
    public async Task Batch_reads_cache_misses_with_the_given_expiry()
    {
        var cache = Create();

        await foreach (var _ in cache.GetAsync(From(Pair<string?>("k", "v")), 20, Ct)) { }

        await _store.Received(1).SetAsync("k", Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromSeconds(20)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Values_are_stored_as_json()
    {
        var cache = Create();

        await cache.SetAsync("k", new byte[] { 1, 2 }, cancellationToken: Ct);

        await _store.Received(1).SetAsync("k", Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "\"AQI=\""),
            Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Key_enumeration_is_not_supported()
    {
        var cache = Create();

        Assert.Throws<NotSupportedException>(() => cache.RemoveByPattern("*"));
        Assert.Throws<NotSupportedException>(cache.Clear);
        await Assert.ThrowsAsync<NotSupportedException>(() => cache.RemoveByPatternAsync("*", Ct));
        await Assert.ThrowsAsync<NotSupportedException>(() => cache.ClearAsync(Ct));
    }

    [Fact]
    public void Dispose_leaves_the_store_alone()
    {
        Create().Dispose();
        Assert.Empty(_store.ReceivedCalls());
    }

    [Fact]
    public void Registration_with_a_configuration_object_wires_the_sql_store()
    {
        var services = new ServiceCollection();
        services.AddSqlServerCacheManager(new SqlServerConfiguration
        {
            ConnectionString = "Server=.;Database=cache",
            SchemaName = "dbo",
            TableName = "Cache",
            DefaultSlidingExpiration = TimeSpan.FromMinutes(5),
            ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(10),
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SqlServerCacheOptions>>().Value;
        Assert.Equal("Server=.;Database=cache", options.ConnectionString);
        Assert.Equal("dbo", options.SchemaName);
        Assert.Equal("Cache", options.TableName);
        Assert.Equal(TimeSpan.FromMinutes(5), options.DefaultSlidingExpiration);
        Assert.Equal(TimeSpan.FromMinutes(10), options.ExpiredItemsDeletionInterval);
        AssertSingleManager(provider);
    }

    [Fact]
    public void Registration_with_a_delegate_wires_the_sql_store()
    {
        using var provider = new ServiceCollection()
            .AddSqlServerCacheManager(o => { o.ConnectionString = "Server=.;Database=cache"; o.SchemaName = "dbo"; o.TableName = "Cache"; })
            .BuildServiceProvider();

        Assert.Equal("Cache", provider.GetRequiredService<IOptions<SqlServerCacheOptions>>().Value.TableName);
        AssertSingleManager(provider);
    }

    [Fact]
    public void The_default_configuration_matches_the_documented_values()
    {
        var configuration = new SqlServerConfiguration();

        Assert.Equal(TimeSpan.FromMinutes(20), configuration.DefaultSlidingExpiration);
        Assert.Equal(TimeSpan.FromMinutes(30), configuration.ExpiredItemsDeletionInterval);
    }

    private static void AssertSingleManager(ServiceProvider provider)
    {
        var manager = provider.GetRequiredService<SqlCacheManager>();
        Assert.Same(manager, provider.GetRequiredService<ICacheManager>());
        Assert.Same(manager, provider.GetRequiredService<IAsyncCacheManager>());
        Assert.Same(manager, provider.GetRequiredService<ISyncCacheManager>());
    }
}
