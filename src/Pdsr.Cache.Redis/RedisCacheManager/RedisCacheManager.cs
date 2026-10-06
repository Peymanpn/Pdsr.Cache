using Pdsr.Cache.Configurations;
using Pdsr.Cache.Internal;
using StackExchange.Redis.KeyspaceIsolation;
using System.Text.Json;

namespace Pdsr.Cache;

/// <summary>
/// <see cref="ICacheManager"/> backed by Redis. Values are stored as JSON strings.
/// </summary>
/// <remarks>
/// When Redis is unreachable, reads, writes and existence checks throw
/// (<see cref="RedisConnectionException"/> or <see cref="RedisTimeoutException"/>) rather than reporting a miss.
/// The read-through <c>Get</c>/<c>GetAsync</c> overloads that take an <c>acquire</c> delegate instead return
/// the acquired value without caching it.
/// The connection is owned by <see cref="IRedisConnectionFactory"/>; disposing the manager leaves it open.
/// <para>
/// StackExchange.Redis can't cancel a command once it is sent. Cancelling a token stops the caller waiting,
/// but the command still reaches Redis, so a cancelled write may still be applied.
/// </para>
/// </remarks>
public partial class RedisCacheManager : IRedisCacheManager
{
    private readonly IRedisConnectionFactory _connectionFactory;
    private readonly IRedisConfiguration _configuration;
    private readonly string _keyPrefix;

    /// <summary>
    /// Creates a cache manager over a shared connection.
    /// </summary>
    public RedisCacheManager(IRedisConfiguration redisConfiguration, IRedisConnectionFactory redisConnectionFactory)
    {
        _configuration = redisConfiguration ?? throw new ArgumentNullException(nameof(redisConfiguration));
        _connectionFactory = redisConnectionFactory ?? throw new ArgumentNullException(nameof(redisConnectionFactory));
        _keyPrefix = redisConfiguration.KeyPrefix ?? string.Empty;
    }

    ///<inheritdoc/>
    public IDatabase Redis => Scope(_connectionFactory.Connection());

    /// <inheritdoc/>
    public ISubscriber Subscriber => _connectionFactory.Connection().GetSubscriber();

    /// <inheritdoc/>
    public bool IsSet(string key) => Redis.KeyExists(key);

    /// <inheritdoc/>
    public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default)
        => ExecuteAsync(db => db.KeyExistsAsync(key), cancellationToken);

    /// <inheritdoc/>
    public void Remove(string key) => Redis.KeyDelete(key);

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => ExecuteAsync(db => db.KeyDeleteAsync(key), cancellationToken);

    /// <inheritdoc/>
    public TimeSpan? GetItemTimeSpanToLive(string key) => Redis.KeyTimeToLive(key);

    /// <inheritdoc/>
    public long GetItemTimeToLive(string key) => ToSeconds(GetItemTimeSpanToLive(key));

    /// <inheritdoc/>
    public Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default)
        => ExecuteAsync(db => db.KeyTimeToLiveAsync(key), cancellationToken);

    /// <inheritdoc/>
    public async Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
        => ToSeconds(await GetItemTimeSpanToLiveAsync(key, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// The manager doesn't own the connection, so this is a no-op; dispose the <see cref="IRedisConnectionFactory"/> instead.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to release resources owned by a derived class.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
    }

    #region Utilities

    private IDatabase Scope(IConnectionMultiplexer connection)
    {
        var database = connection.GetDatabase(_configuration.Database);
        return _keyPrefix.Length == 0 ? database : database.WithKeyPrefix(_keyPrefix);
    }

    private async Task<IDatabase> DatabaseAsync(CancellationToken cancellationToken)
        => Scope(await _connectionFactory.ConnectionAsync(cancellationToken).ConfigureAwait(false));

    private async Task<TResult> ExecuteAsync<TResult>(Func<IDatabase, Task<TResult>> command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = await DatabaseAsync(cancellationToken).ConfigureAwait(false);
        return await command(database).WithCancellation(cancellationToken).ConfigureAwait(false);
    }

    private static long ToSeconds(TimeSpan? ttl) => ttl.HasValue ? (long)ttl.Value.TotalSeconds : -1;

    private static string Serialize<T>(T data) => JsonSerializer.Serialize(data);

    private static T? Deserialize<T>(RedisValue value) => JsonSerializer.Deserialize<T>((string)value!);

    private static bool IsUnavailable(Exception exception)
        => exception is RedisConnectionException or RedisTimeoutException;

    #endregion
}
