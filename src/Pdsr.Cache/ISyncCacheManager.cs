namespace Pdsr.Cache;

/// <summary>
/// Synchronous cache manager.
/// </summary>
/// <remarks>
/// Implementations backed by a remote store throw when the store is unreachable, so an outage is
/// never reported as a cache miss. The read-through <see cref="Get{T}(string, Func{T}, int?)"/>
/// is the exception: it falls back to <c>acquire</c> so callers still get a correct value.
/// </remarks>
public interface ISyncCacheManager : IDisposable
{
    /// <summary>
    /// Get a cached item. If it's not in the cache yet, then acquire and cache it.
    /// </summary>
    /// <typeparam name="T">Type of cached item</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="acquire">Function to load the item if it's not in the cache yet</param>
    /// <param name="cacheTime">Cache time in seconds; pass null to cache indefinitely</param>
    /// <returns>The cached value associated with the specified key</returns>
    T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null);

    /// <summary>
    /// Gets a cached item, or <c>default</c> when it is not cached.
    /// A cached <c>default</c> and a miss look the same here; use <see cref="TryGet{T}"/> to tell them apart.
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// Reads a cached item in a single operation.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">The cached value when found; otherwise <c>default</c></param>
    /// <returns>True on a cache hit; false on a miss</returns>
    bool TryGet<T>(string key, out T? value);

    /// <summary>
    /// Adds or replaces the specified key and object in the cache. Null values are not cached.
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <param name="data">Value for caching</param>
    /// <param name="cacheTime">Cache time in seconds; null caches indefinitely</param>
    void Set<T>(string key, T? data, int? cacheTime = null);

    /// <summary>
    /// Adds or replaces the specified key and object in the cache. Null values are not cached.
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <param name="data">Value for caching</param>
    /// <param name="expiry">Time to live; null caches indefinitely</param>
    void Set<T>(string key, T? data, TimeSpan? expiry);

    /// <summary>
    /// Gets a value indicating whether the value associated with the specified key is cached.
    /// Don't follow this with a separate read: the key can expire in between. Use <see cref="TryGet{T}"/> instead.
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <returns>True if item already is in cache; otherwise false</returns>
    bool IsSet(string key);

    /// <summary>
    /// Removes the value with the specified key from the cache
    /// </summary>
    /// <param name="key">Key of cached item</param>
    void Remove(string key);

    /// <summary>
    /// Removes items by key pattern
    /// </summary>
    /// <param name="pattern">String key pattern</param>
    void RemoveByPattern(string pattern);

    /// <summary>
    /// Clear all cache data
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets seconds remaining until key expires.
    /// Returns -1 when the key does not exist or has no expiry.
    /// </summary>
    /// <param name="key">Cache Key</param>
    /// <returns>Total seconds</returns>
    /// <remarks>http://redis.io/commands/ttl</remarks>
    long GetItemTimeToLive(string key);

    /// <summary>
    /// Time to live, or null when the key does not exist or has no expiry.
    /// </summary>
    /// <param name="key">Cache Key</param>
    /// <remarks>http://redis.io/commands/ttl</remarks>
    TimeSpan? GetItemTimeSpanToLive(string key);
}
