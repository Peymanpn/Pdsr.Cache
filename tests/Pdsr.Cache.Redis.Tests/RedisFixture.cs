using Pdsr.Cache.Configurations;
using RedisBuilder = Testcontainers.Redis.RedisBuilder;
using RedisContainer = Testcontainers.Redis.RedisContainer;

[assembly: AssemblyFixture(typeof(Pdsr.Cache.Redis.Tests.RedisFixture))]

namespace Pdsr.Cache.Redis.Tests;

/// <summary>
/// Two independent Redis servers in Docker, shared by every integration test in the assembly.
/// Tests isolate themselves with a unique key prefix.
/// Without Docker, integration tests are skipped locally and fail on CI.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _primary;
    private RedisContainer? _secondary;

    public string PrimaryEndPoint { get; private set; } = "";

    public string SecondaryEndPoint { get; private set; } = "";

    public string? UnavailableReason { get; private set; }

    /// <summary>Shared connection to the primary server.</summary>
    public RedisConnectionFactory Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _primary = new RedisBuilder("redis:7-alpine").Build();
            _secondary = new RedisBuilder("redis:7-alpine").Build();
            await Task.WhenAll(_primary.StartAsync(), _secondary.StartAsync());
            PrimaryEndPoint = _primary.GetConnectionString();
            SecondaryEndPoint = _secondary.GetConnectionString();
            Factory = new RedisConnectionFactory(Configuration());
        }
        catch (Exception ex) when (Environment.GetEnvironmentVariable("CI") != "true")
        {
            UnavailableReason = $"Docker is not available: {ex.Message}";
        }
    }

    /// <summary>Skips the calling test when Docker isn't available.</summary>
    public void RequireDocker() => Assert.SkipWhen(UnavailableReason is not null, UnavailableReason ?? "");

    public RedisConfiguration Configuration(Action<RedisConfiguration>? configure = null)
    {
        var configuration = new RedisConfiguration { EndPoints = [PrimaryEndPoint], AbortOnConnectFail = true };
        configure?.Invoke(configuration);
        return configuration;
    }

    /// <summary>A cache manager over the shared connection with its own key prefix.</summary>
    public RedisCacheManager CreateCache(string? keyPrefix = null)
        => new(Configuration(c => c.KeyPrefix = keyPrefix ?? $"test:{Guid.NewGuid():N}:"), Factory);

    public async ValueTask DisposeAsync()
    {
        Factory?.Dispose();
        if (_primary is not null) await _primary.DisposeAsync();
        if (_secondary is not null) await _secondary.DisposeAsync();
    }
}
