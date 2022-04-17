namespace Pdsr.Cache;

/// <summary>
/// Cache manager interface
/// </summary>
public interface ISyncCacheManager : IDisposable
{
    /// <summary>
    /// Get a cached item. If it's not in the cache yet, then acquire and cache it
    /// </summary>
    /// <typeparam name="T">Type of cached item</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="acquire">Function to load item if it's not in the cache yet</param>
    /// <param name="cacheTime">Cache time in seconds; pass 0 to do not cache; pass null to use the default time</param>
    /// <returns>The cached value associated with the specified key</returns>
    T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null);
    //string Get(string key, Func<string> acquire, int? cacheTime = null);
    // int Get(string key, Func<int> acquire, int? cacheTime = null);
    // long Get(string key, Func<long> acquire, int? cacheTime = null);
    // byte[] Get(string key, Func<byte[]> acquire, int? cacheTime = null);
    T? Get<T>(string key);


    /// <summary>
    /// Adds the specified key and object to the cache
    /// </summary>
    /// <param name="key">Key of cached item</param>
    /// <param name="data">Value for caching</param>
    /// <param name="cacheTime">Cache time in seconds</param>
    //void Set(string key, object data, int? cacheTime = null);

    //void Set(string key, object data, TimeSpan? expiry = null);
    //void Set(string key, int data, int? cacheTime = null);
    //void Set(string key, string data, int? cacheTime = null);
    //void Set(string key, long data, int? cacheTime = null);
    //void Set(string key, byte[] data, int? cacheTime = null);


    void Set<T>(string key, T? data, int? cacheTime = null);
    // void Set<T>(string key, T data, TimeSpan? expiry = null);


    /// <summary>
    /// Gets a value indicating whether the value associated with the specified key is cached
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
    /// Gets seconds remaining until key expires.TTL in seconds, or nil when key does not exist or does not have a timeout
    /// returns -1 if has no expire date
    /// </summary>
    /// <param name="key">Cache Key</param>
    /// <returns>Total seconds</returns>
    /// <remarks>http://redis.io/commands/ttl</remarks>
    long GetItemTimeToLive(string key);

    /// <summary>
    /// TTL, or nil when key does not exist or does not have a timeout.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <remarks>http://redis.io/commands/ttl</remarks>
    TimeSpan? GetItemTimeSpanToLive(string key);
}
