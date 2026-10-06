namespace Pdsr.Cache;

/// <summary>
/// Asynchronous cache manager.
/// </summary>
/// <remarks>
/// Implementations backed by a remote store throw when the store is unreachable, so an outage is
/// never reported as a cache miss. The read-through <c>GetAsync</c> overloads that take an
/// <c>acquire</c> delegate are the exception: they fall back to <c>acquire</c> so callers still get a correct value.
/// <para>
/// Cancellation stops the caller waiting: the call throws <see cref="OperationCanceledException"/>.
/// It does not undo work already sent to a remote store. A command sent before the token fired may still run,
/// so a cancelled <c>SetAsync</c> or <c>RemoveAsync</c> can still write or delete the key.
/// A token that is already cancelled is rejected before anything is sent.
/// </para>
/// </remarks>
public interface IAsyncCacheManager : IDisposable
{
    /// <summary>
    /// Get a cached item asynchronously. If it's not in the cache yet, then load and cache it
    /// </summary>
    /// <typeparam name="T">Type of cached item</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="acquire">Function to acquire data if it's not in the cache yet</param>
    /// <param name="cacheTime">Cache time in seconds. Pass null to cache indefinitely</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached value associated with the specified key.
    /// If cached data does not exist and the result of <paramref name="acquire"/> is null, returns null.
    /// </returns>
    Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a cached item asynchronously. If it's not in the cache yet, await <paramref name="acquireTask"/> and cache its result.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="acquireTask">Task producing the data. It is already running, so prefer the <c>Func&lt;Task&gt;</c> overload.</param>
    /// <param name="cacheTime">Cache time in seconds. Pass null to cache indefinitely</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<T?> GetAsync<T>(string key, Task<T?> acquireTask, int? cacheTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a cached item asynchronously with a synchronous acquire method.
    /// If it doesn't exist in the cache, <paramref name="acquire"/> is invoked and its result cached.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="acquire">Method to invoke if the item isn't cached</param>
    /// <param name="cacheTime">Cache time in seconds. Pass null to cache indefinitely</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached item, or <c>default</c> when it is not cached.
    /// A cached <c>default</c> and a miss look the same here; use <see cref="TryGetAsync{T}"/> to tell them apart.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a cached item in a single operation.
    /// </summary>
    /// <returns>A hit carrying the cached value, or <see cref="CacheResult{T}.Miss"/></returns>
    Task<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Read-through for a stream of keys; each miss is acquired and cached.</summary>
    IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);

    /// <summary>Read-through for a stream of keys; each miss is acquired and cached.</summary>
    IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);

    /// <summary>Read-through for a stream of keys; each miss is filled with the supplied value.</summary>
    IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or replaces the specified key and object in the cache. Null values are not cached.
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <param name="data">Value for caching</param>
    /// <param name="cacheTime">Cache time in seconds; null caches indefinitely</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or replaces the specified key and object in the cache. Null values are not cached.
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <param name="data">Value for caching</param>
    /// <param name="expiry">Time to live; null caches indefinitely</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default);

    /// <summary>Caches every non-null value in the stream. Completes when all writes have finished.</summary>
    Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);

    /// <summary>Caches every non-null value in the stream. Completes when all writes have finished.</summary>
    Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);

    /// <summary>Caches every non-null value in the stream. Completes when all writes have finished.</summary>
    Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the value associated with the specified key is cached.
    /// Don't follow this with a separate read: the key can expire in between. Use <see cref="TryGetAsync{T}"/> instead.
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if item already is in cache; otherwise false</returns>
    Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the value with the specified key from the cache
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes items by key pattern
    /// </summary>
    /// <param name="pattern">String key pattern</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cache data
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets seconds remaining until key expires.
    /// Returns -1 when the key does not exist or has no expiry.
    /// </summary>
    /// <param name="key">Cache Key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total seconds</returns>
    /// <remarks>http://redis.io/commands/ttl</remarks>
    Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Time to live, or null when the key does not exist or has no expiry.
    /// </summary>
    /// <param name="key">Cache Key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>http://redis.io/commands/ttl</remarks>
    Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default);
}
