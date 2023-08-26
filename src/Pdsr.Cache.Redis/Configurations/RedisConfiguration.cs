namespace Pdsr.Cache.Configurations;

/// <summary>
/// Redis Configurations for setting up RedisConnectionFactory, <see cref="RedisConnectionFactory"/>
/// </summary>
public class RedisConfiguration : IRedisConfiguration
{
    /// <summary>
    /// Host and Port will be translated to Endpoints.
    /// if <see cref="EndPoints"/> provided, <see cref="Host"/> and <see cref="Port"/> will be ignored
    /// </summary>
    [Obsolete("Use Endpoints instead", true)]
    public string? Host { get; set; }

    /// <summary>
    /// Host and Port will be translated to Endpoints.
    /// if <see cref="EndPoints"/> provided, <see cref="Host"/> and <see cref="Port"/> will be ignored
    /// </summary>
    [Obsolete("Use Endpoints instead", true)]
    public int? Port { get; set; }

    /// <summary>
    /// Multiple Endpoints for Redis Servers
    /// </summary>
    public string[] EndPoints { get; set; } = new string[0];


    // public string Name { get; set; }

    /// <summary>
    /// Use SSL to connect to Redis
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Client Name to use for all connections
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Use the assembly name as client name
    /// </summary>
    public bool UseCallingAssemblyNameAsClientName { get; set; } = true;

    /// <summary>
    /// Indicates whether admin operations should be allowed
    /// </summary>
    public bool AllowAdmin { get; set; }

    /// <summary>
    ///  Gets or sets whether connect/configuration timeouts should be explicitly notified
    ///  via a TimeoutException
    /// </summary>
    public bool AbortOnConnectFail { get; set; }

    /// <summary>
    /// User for connecting to redis
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Password for connecting to redis
    /// </summary>
    public string? Password { get; set; }


    // TODO: move to Redis.Polly
    /// <summary>
    /// Total Retry Count Before give up
    /// </summary>
    public int RetryCount { get; set; } = 10;
}
