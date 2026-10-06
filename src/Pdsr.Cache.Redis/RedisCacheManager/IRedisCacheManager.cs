namespace Pdsr.Cache;

/// <summary>
/// Extended actions and methods for Redis
/// </summary>
public interface IRedisCacheManager : ICacheManager
{
    /// <summary>
    /// Direct access to the Redis database, scoped to <see cref="Configurations.IRedisConfiguration.KeyPrefix"/>
    /// when one is configured. Should be avoided in most scenarios.
    /// </summary>
    IDatabase Redis { get; }

    /// <summary>
    /// A connected primary server. With several primaries this is only one of them.
    /// </summary>
    IServer Server { get; }

    /// <summary>
    /// Redis pub/sub
    /// </summary>
    ISubscriber Subscriber { get; }

    /// <summary>
    /// All keys stored by this cache manager, across every primary server, without the key prefix.
    /// </summary>
    IEnumerable<string> Keys { get; }

    /// <summary>
    /// Gets all keys for a user, assuming the <c>????:subjectId:*</c> pattern
    /// </summary>
    /// <param name="subjectId">The user's subject id</param>
    IEnumerable<string> GetKeysForUser(string subjectId);

    /// <summary>
    /// Gets all keys for a user matching <c>{prefix}{subjectId}:*</c>.
    /// The default prefix matches any four characters followed by a colon.
    /// </summary>
    /// <param name="subjectId">The user's subject id</param>
    /// <param name="prefix">Glob prefix, e.g. <c>abcd:</c></param>
    IEnumerable<string> GetKeysForUserByPrefix(string subjectId, string prefix = "????:");

    /// <summary>
    /// Gets a cached item, optionally from a replica.
    /// </summary>
    /// <param name="key">the cache key</param>
    /// <param name="preferReplica">If true, reads from a replica when one is available</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The cached item, or <c>default</c> when it isn't cached</returns>
    Task<T?> GetAsync<T>(string key, bool preferReplica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached item, optionally from a replica.
    /// </summary>
    /// <param name="key">the cache key</param>
    /// <param name="preferReplica">If true, reads from a replica when one is available</param>
    /// <returns>The cached item, or <c>default</c> when it isn't cached</returns>
    T? Get<T>(string key, bool preferReplica);

    #region Sets

    /// <summary>
    /// Returns the members of a Redis set. Each member is the key of another cache item.
    /// </summary>
    /// <param name="setKeyName">Set's name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IEnumerable<string>> GetSetAsync(string setKeyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the cache items whose keys are members of the set.
    /// For a set holding {"A","B"} this reads the items <c>{prefix}A</c> and <c>{prefix}B</c>.
    /// Members whose item has expired are left out.
    /// </summary>
    /// <param name="setKeyName">Name of the set which holds the cache item keys</param>
    /// <param name="cacheItemKeyPrefix">Prefix prepended to each member to form the item key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IEnumerable<T?>> GetSetItemsAsync<T>(string setKeyName, string? cacheItemKeyPrefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <paramref name="key"/> to the set and caches <paramref name="value"/> under <c>{prefix}{key}</c>.
    /// </summary>
    /// <param name="setKeyName">The set holding cache item keys</param>
    /// <param name="key">Value's cache key</param>
    /// <param name="value">The object to store</param>
    /// <param name="cacheItemKeyPrefix">Prefix prepended to <paramref name="key"/> to form the item key</param>
    /// <param name="cacheTime">Cache item expiration in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddToSetAsync<T>(string setKeyName, string key, T value, string? cacheItemKeyPrefix = null, int? cacheTime = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes <paramref name="key"/> from the set and deletes the item <c>{prefix}{key}</c>.
    /// </summary>
    /// <param name="setKeyName">The set holding cache item keys</param>
    /// <param name="key">The member to remove</param>
    /// <param name="cacheItemKeyPrefix">Prefix prepended to <paramref name="key"/> to form the item key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveFromSetAsync(string setKeyName, string key, string? cacheItemKeyPrefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of members in a set
    /// </summary>
    /// <param name="setKeyName">Key of Set</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<long> GetSetLengthAsync(string setKeyName, CancellationToken cancellationToken = default);

    #endregion
}
