namespace Pdsr.Cache.Configurations;

/// <summary>
/// Settings used by <see cref="RedisConnectionFactory"/> and <see cref="RedisCacheManager"/>.
/// </summary>
public interface IRedisConfiguration
{
    /// <summary>
    /// Redis servers as <c>host:port</c>, e.g. <c>redis.internal:6379</c>, <c>10.0.0.5:6379</c> or <c>[::1]:6379</c>.
    /// Host names are kept as names: they are re-resolved on reconnect and used for TLS certificate validation.
    /// The port defaults to 6379 (6380 with TLS) when omitted.
    /// </summary>
    string[] EndPoints { get; set; }

    /// <summary>
    /// Use TLS to connect to Redis
    /// </summary>
    bool UseSsl { get; set; }

    /// <summary>
    /// Client name reported to the server. When null and <see cref="UseEntryAssemblyNameAsClientName"/> is set,
    /// the entry assembly name is used.
    /// </summary>
    string? ClientName { get; set; }

    /// <summary>
    /// Use the entry assembly name as client name when <see cref="ClientName"/> is not set
    /// </summary>
    bool UseEntryAssemblyNameAsClientName { get; set; }

    /// <summary>
    /// Allows admin commands. Not needed for caching; <see cref="AllowFlushDatabase"/> needs it.
    /// </summary>
    bool AllowAdmin { get; set; }

    /// <summary>
    /// When true, the first connect throws if the server can't be reached.
    /// When false, the connection is established in the background and commands fail until it is up.
    /// </summary>
    bool AbortOnConnectFail { get; set; }

    /// <summary>
    /// Username (Redis 6 ACL)
    /// </summary>
    string? User { get; set; }

    /// <summary>
    /// Password for connecting to redis
    /// </summary>
    string? Password { get; set; }

    /// <summary>
    /// Number of times to retry the initial connect.
    /// </summary>
    int RetryCount { get; set; }

    /// <summary>
    /// Database index; -1 uses the server default.
    /// </summary>
    int Database { get; set; }

    /// <summary>
    /// Optional prefix applied to every key this cache manager reads or writes, e.g. <c>orders:</c>.
    /// Gives each application or section sharing a server its own keyspace: pattern removal and
    /// <see cref="ISyncCacheManager.Clear"/> then only touch keys under this prefix.
    /// </summary>
    string? KeyPrefix { get; set; }

    /// <summary>
    /// Lets <see cref="ISyncCacheManager.Clear"/> run FLUSHDB when no <see cref="KeyPrefix"/> is set.
    /// That deletes every key in the database, including other applications' keys, so it is off by default.
    /// Requires <see cref="AllowAdmin"/>.
    /// </summary>
    bool AllowFlushDatabase { get; set; }
}
