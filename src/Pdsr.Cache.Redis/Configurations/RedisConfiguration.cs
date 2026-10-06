namespace Pdsr.Cache.Configurations;

/// <summary>
/// Redis Configurations for setting up <see cref="RedisConnectionFactory"/>
/// </summary>
public class RedisConfiguration : IRedisConfiguration
{
    /// <inheritdoc/>
    public string[] EndPoints { get; set; } = [];

    /// <inheritdoc/>
    public bool UseSsl { get; set; }

    /// <inheritdoc/>
    public string? ClientName { get; set; }

    /// <inheritdoc/>
    public bool UseEntryAssemblyNameAsClientName { get; set; } = true;

    /// <inheritdoc/>
    public bool AllowAdmin { get; set; }

    /// <inheritdoc/>
    public bool AbortOnConnectFail { get; set; }

    /// <inheritdoc/>
    public string? User { get; set; }

    /// <inheritdoc/>
    public string? Password { get; set; }

    /// <inheritdoc/>
    public int RetryCount { get; set; } = 3;

    /// <inheritdoc/>
    public int Database { get; set; } = -1;

    /// <inheritdoc/>
    public string? KeyPrefix { get; set; }

    /// <inheritdoc/>
    public bool AllowFlushDatabase { get; set; }
}
