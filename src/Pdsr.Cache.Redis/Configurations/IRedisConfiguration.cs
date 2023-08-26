namespace Pdsr.Cache.Configurations;

public interface IRedisConfiguration
{
    /// <summary>
    /// Host and Port will be translated to Endpoints.
    /// if <see cref="EndPoints"/> provided, <see cref="Host"/> and <see cref="Port"/> will be ignored
    /// </summary>
    [Obsolete("Use Endpoints instead", true)]
    string? Host { get; set; }

    /// <summary>
    /// Host and Port will be translated to Endpoints.
    /// if <see cref="EndPoints"/> provided, <see cref="Host"/> and <see cref="Port"/> will be ignored
    /// </summary>
    [Obsolete("Use Endpoints instead", true)]
    int? Port { get; set; }

    /// <summary>
    /// Multiple Endpoints for Redis Servers
    /// </summary>
    string[] EndPoints { get; set; }


    // public string Name { get; set; }

    /// <summary>
    /// Use SSL to connect to Redis
    /// </summary>
    bool UseSsl { get; set; }

    /// <summary>
    /// Client Name to use for all connections
    /// </summary>
    string? ClientName { get; set; }

    /// <summary>
    /// Use the assembly name as client name
    /// </summary>
    bool UseCallingAssemblyNameAsClientName { get; set; }

    /// <summary>
    /// Indicates whether admin operations should be allowed
    /// </summary>
    bool AllowAdmin { get; set; }

    /// <summary>
    ///  Gets or sets whether connect/configuration timeouts should be explicitly notified
    ///  via a TimeoutException
    /// </summary>
    bool AbortOnConnectFail { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    string? User { get; set; }

    /// <summary>
    /// Password for connecting to redis
    /// </summary>
    string? Password { get; set; }

    // TODO: move to Redis.Polly
    /// <summary>
    /// Total Retry Count Before give up
    /// </summary>
    // int RetryCount { get; set; } = 10;
}
