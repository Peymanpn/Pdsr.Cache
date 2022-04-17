namespace Pdsr.Cache;

/// <summary>
/// Asynchronous Cache Manager interface
/// </summary>
public interface IAsyncCacheManager : IDisposable
{
    /// <summary>
    /// Get a cached item asynchronously. If it's not in the cache yet, then load and cache it
    /// </summary>
    /// <typeparam name="T">Type of cached item</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="acquire">Function to acquire date if it's not in the cache yet</param>
    /// <param name="cacheTime">Cache time in seconds. Pass null to cache indefenitely</param>
    /// <returns>The cached value associated with the specified key.
    /// If cached data does not exists and results of <paramref name="acquireTask"/> is null, returns null.
    /// </returns>
    Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a cached item asynchronously. If it's not in the cache yet, then load and cache it
    /// </summary>
    /// <param name="key"></param>
    /// <param name="acquireTask">Task containing work to retreive the data.</param>
    /// <param name="cacheTime">Cache time in seconds. Pass null to cache indefenitely</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached value associated with the specified key.
    /// If cached data does not exists and results of <paramref name="acquireTask"/> is null, returns null.
    /// </returns>
    Task<T?> GetAsync<T>(string key, Task<T?> acquireTask, int? cacheTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a cached Item Asynchronously with synchronous acquireTask method.
    /// if it doesnt exists in cache, it will acquireTask with provided method.
    /// </summary>
    /// <typeparam name="T">Generic Type of method, can be any class with default constructor</typeparam>
    /// <param name="key"></param>
    /// <param name="acquire">method to invoke if cache doesn't exists</param>
    /// <param name="cacheTime">Cache time in seconds. Pass null to cache indefenitely</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached value associated with the specified key</returns>
    Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets from cache.if it's not available, returns <see cref="default"/>
    /// </summary>
    /// <typeparam name="T">Type of object to deserialize when getting from cache.</typeparam>
    /// <param name="key">the cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>returns the cached itm and will return null if cache item not available</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);
    /// <summary>
    /// Adds the specified key and object to the cache asynchronously
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <param name="data">Value for caching</param>
    /// <param name="cacheTime">Cache time in seconds</param>
    //Task SetAsync(string key, object data, TimeSpan? expiry = null, CancellationToken cancellationToken = default(CancellationToken));
    //Task SetAsync(string key, object data, int? cacheTime = null, CancellationToken cancellationToken = default);

    // Task SetAsync(string key, byte[] data, int? cacheTime = null, CancellationToken cancellationToken = default);
    //Task SetAsync(string key, byte[] data, TimeSpan? cacheTime = null, CancellationToken cancellationToken = default(CancellationToken));
    Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default);




    Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);
    Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);
    Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default);





    /// <summary>
    /// Gets a value indicating whether the value associated with the specified key is cached
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <returns>True if item already is in cache; otherwise false</returns>
    Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes the value with the specified key from the cache
    /// </summary>
    /// <param name="key">Key of cached item</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes items by key pattern
    /// </summary>
    /// <param name="pattern">String key pattern</param>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all cache data
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets seconds remaining until key expires
    /// returns -1 if has no expire date.
    /// TTL in seconds, or nil when key does not exist or does not have a timeout.
    /// </summary>
    /// <param name="key">Cache Key</param>
    /// <returns>Total seconds</returns>
    /// <remarks>http://redis.io/commands/ttl</remarks>
    Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///  Returns the remaining time to live of a key that has a timeout. This introspection
    ///     capability allows a Redis client to check how many seconds a given key will continue
    ///     to be part of the dataset.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>TTL, or nil when key does not exist or does not have a timeout.</returns>
    /// <remarks>http://redis.io/commands/ttl</remarks>
    Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default);


}
