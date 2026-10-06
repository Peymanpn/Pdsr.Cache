namespace Pdsr.Cache;

/// <summary>
/// Owns the shared Redis connection. Cache managers borrow it and never dispose it.
/// </summary>
public interface IRedisConnectionFactory : IDisposable
{
    /// <summary>
    /// Returns the shared connection, connecting synchronously on first use.
    /// </summary>
    IConnectionMultiplexer Connection();

    /// <summary>
    /// Returns the shared connection, connecting asynchronously on first use.
    /// </summary>
    Task<IConnectionMultiplexer> ConnectionAsync(CancellationToken cancellationToken = default);
}
