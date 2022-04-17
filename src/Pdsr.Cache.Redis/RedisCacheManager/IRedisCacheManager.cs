namespace Pdsr.Cache;

/// <summary>
/// Extended actions and methods for Redis
/// </summary>
public interface IRedisCacheManager : ICacheManager
{
    /// <summary>
    /// Give access to the Redis database directly. should be avoided in most scenarios
    /// </summary>
    IDatabase Redis { get; }

    /// <summary>
    /// Give access to the server holding database directly.
    /// until version 3, it still won't support Distributed access to all servers
    /// </summary>
    IServer Server { get; }

    /// <summary>
    /// Give access to Redis  Subscripton/Publisher 
    /// </summary>
    ISubscriber Subscriber { get; }

    /// <summary>
    /// List of keys stored on server
    /// </summary>
    IEnumerable<string> Keys { get; }

    /// <summary>
    /// Gets all keys for a user, assuming *:subjectId:* pattern
    /// </summary>
    /// <param name="subjectId">a 32 character string (Guid) of the users</param>
    /// <returns>Collection of Keys associated by specifiec user</returns>
    IEnumerable<string> GetKeysForUser(string subjectId);

    /// <summary>
    /// Gets all keys associated with user, based on user's subject id
    /// normally prefix contains 4 characters like abcd:subjectId:key if you dont specificely determine prefix,
    /// it will match any 4 characters.
    /// format can be defined in <see cref="RedisConfiguration.KeyFormat"/>
    /// </summary>
    /// <param name="subjectId">a 32 character string (Guid) of the users</param>
    /// <param name="prefix">prefix contains 4 characters like abcd:subjectId:key</param>
    /// <returns>Collection of Keys associated by specifiec user</returns>
    IEnumerable<string> GetKeysForUserByPrefix(string subjectId, string prefix = "????:");


    /// <summary>
    /// Gets from cache.if it's not available, returns <see cref="default"/>
    /// </summary>
    /// <param name="key">the cache key</param>
    /// <param name="preferReplica">If true, it reads from replica server, instead of main</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>returns the cached itm and will return null if cache item not available</returns>
    Task<T?> GetAsync<T>(string key, bool preferReplica = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets from cache.if it's not available, returns <see cref="default"/>
    /// </summary>
    /// <param name="key">the cache key</param>
    /// <param name="preferReplica">If true, it reads from replica server, instead of main</param>
    /// <returns>returns the cached itm and will return null if cache item not available</returns>
    T? Get<T>(string key, bool preferReplica = true);


    #region Sets

    /// <summary>
    /// Returns List of cache keys as set items
    /// each item is another cache key
    /// </summary>
    /// <param name="setKeyName">Set's name</param>
    /// <param name="cancellationToken">The canceled state for the token.</param>
    /// <returns>Returns a list of strings representing other cache keys</returns>
    Task<IEnumerable<string>> GetSetAsync(string setKeyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns actual other cache items from the list of items stored in set
    /// for example if a set contains {"A","B","C"}, this method will retrieve cache items with name "A", "B" and "C"
    /// you can also provide a prefix to cache keys for retrieving
    /// </summary>
    /// <param name="setKeyName">Name of the set which holds these cache items</param>
    /// <param name="cancellationToken">The canceled state for the token.</param>
    /// <returns>returns cache items stored in this particular set</returns>
    Task<IEnumerable<T?>> GetSetItemsAsync<T>(string setKeyName, string? cacheItemKeyPrefix = null, CancellationToken cancellationToken = default);
    //IAsyncEnumerable<T> GetSetItemsIAsync<T>(string setKeyName, string cacheItemKeyPrefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds and Item to the Set and also as a cache item
    /// </summary>
    /// <param name="setKeyName">the Set to hold cache item keys</param>
    /// <param name="key">vakue's cache key</param>
    /// <param name="value">the object to store</param>
    /// <param name="cacheTime">cache item expiration in seconds</param>
    /// <param name="cancellationToken">The canceled state for the token.</param>
    /// <returns></returns>
    Task AddToSetAsync<T>(string setKeyName, string key, T value, string? cacheItemKeyPrefix = null, int? cacheTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an Item from set and the item from cache
    /// </summary>
    /// <param name="setKeyName"></param>
    /// <param name="keyName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task RemoveFromSetItemAsync(string setKeyName, string keyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Length of set items
    /// </summary>
    /// <param name="setKeyName">Key of Set</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<long> GetSetLength(string setKeyName, CancellationToken cancellationToken = default);

    #endregion
}
