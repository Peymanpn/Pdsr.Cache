namespace Pdsr.Cache.Configurations;

/// <summary>
/// Optional settings for how long commands wait, especially while Redis is unreachable.
/// <see cref="RedisConfiguration"/> implements this. A custom <see cref="IRedisConfiguration"/> can implement it too;
/// one that doesn't keeps the StackExchange.Redis defaults.
/// </summary>
/// <remarks>
/// This is a separate interface rather than members on <see cref="IRedisConfiguration"/> so that adding it doesn't break
/// existing implementations: the library also targets netstandard2.0, which has no default interface implementations.
/// </remarks>
public interface IRedisTimeoutConfiguration
{
    /// <summary>
    /// When true, commands issued while no connection is available fail immediately with a
    /// <see cref="RedisConnectionException"/> instead of being queued until <see cref="AsyncTimeout"/> or
    /// <see cref="SyncTimeout"/> expires (5 seconds by default). A cache is an optimisation, so failing fast lets callers
    /// fall back to the source without paying the timeout on every request during an outage.
    /// The trade-off: commands issued during a brief reconnect fail too, instead of being sent once the connection is back.
    /// </summary>
    bool FailFastWhenDisconnected { get; set; }

    /// <summary>
    /// How long an asynchronous command may wait, in milliseconds. Null uses the StackExchange.Redis default (5000).
    /// </summary>
    int? AsyncTimeout { get; set; }

    /// <summary>
    /// How long a synchronous command may wait, in milliseconds. Null uses the StackExchange.Redis default (5000).
    /// </summary>
    int? SyncTimeout { get; set; }
}
