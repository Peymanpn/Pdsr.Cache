using System.Runtime.CompilerServices;

namespace Pdsr.Cache;

public partial class RedisCacheManager : ICacheManager, IRedisCacheManager
{
    #region Internal
    private T? GetFromRedisValue<T>(RedisValue redisValue)
    {
        if (redisValue.HasValue)
            return Deserialize<T>(redisValue.ToString());
        else return default;
    }

    private (RedisValue value, bool disconnected) GetString(RedisKey key, bool demandReplica = false)
    {
        if (!Redis.IsConnected(key, demandReplica ? CommandFlags.PreferReplica : CommandFlags.None))
            return (RedisValue.Null, true);
        else
            return (Redis.StringGet(key, demandReplica ? CommandFlags.PreferReplica : CommandFlags.None), false);
    }
    private async Task<(RedisValue value, bool disconnected)> GetStringAsync(RedisKey key, bool demandReplica = false)
    {
        if (!Redis.IsConnected(key))
            return (RedisValue.Null, true);
        else
            return (await Redis.StringGetAsync(key, demandReplica ? CommandFlags.PreferReplica : CommandFlags.None), false);
    }

    private async Task<T?> GetAsyncInternal<T>(string key, CancellationToken cancellationToken)
    {
        var data = await Redis.StringGetAsync(key);
        if (data.HasValue) return Deserialize<T>(data.ToString());
        return default;
    }

    #endregion

    #region Get Synchronous
    ///<inheritdoc/> 
    public T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null)
    {
        var dbValue = GetString(key);
        if (dbValue.disconnected) return acquire();
        if (dbValue.value.HasValue)
        {
            var value = GetFromRedisValue<T>(dbValue.value);
            if (value == null)
            {
                value = acquire();
                Set(key, value, cacheTime);
            }
            return value;
        }
        else
        {
            var newValue = acquire();
            if (newValue is null) return default;
            Set(key, newValue, cacheTime);
            return newValue;
        }
    }

    ///<inheritdoc/> 
    public T? Get<T>(string key)
    {
        return GetFromRedisValue<T>(GetString(key).value);
    }


    ///<inheritdoc/>
    public T? Get<T>(string key, bool preferReplica = true)
    {
        var (value, disconnected) = GetString(key, preferReplica);
        if (disconnected) return default;
        return GetFromRedisValue<T>(value);
    }


    #endregion

    #region Get Asynchronous 
    ///<inheritdoc/> 
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var (value, disconnected) = await GetStringAsync(key);
        if (disconnected) return default;
        return GetFromRedisValue<T>(value);
    }

    ///<inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, bool preferReplica = true, CancellationToken cancellationToken = default)
    {
        var (value, disconnected) = await GetStringAsync(key, preferReplica);
        if (disconnected) return default;
        return GetFromRedisValue<T>(value);
    }
    ///<inheritdoc/> 
    public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        var (value, disconnected) = await GetStringAsync(key);
        if (disconnected) return await acquire();
        if (value.HasValue) return GetFromRedisValue<T>(value);

        var acquiredValue = await acquire();
        await SetAsync(key, acquiredValue, cacheTime, cancellationToken);
        return acquiredValue;
    }

    /// <inheritdoc/>
    public virtual async Task<T?> GetAsync<T>(string key, Task<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        => await GetAsync<T>(key: key, acquire: () => acquire, cacheTime: cacheTime, cancellationToken: cancellationToken);

    /// <inheritdoc/>
    public virtual async Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        if (!Redis.IsConnected(key)) return acquire();
        var dbValue = await Redis.StringGetAsync(key);
        if (dbValue.HasValue)
        {
            if (dbValue.IsNullOrEmpty) return default;
            else
                return Deserialize<T>(dbValue!);
        }
        else
        {
            T? newValue = acquire();
            if (newValue is null) return default;
            await SetAsync(key, newValue, cacheTime, cancellationToken);
            return newValue;
        }
    }



    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            if (await Redis.KeyExistsAsync(item.Key))
            {
                var results = await GetAsyncInternal<T>(item.Key, cancellationToken);
                yield return results;
            }
            else
            {
                var data = item.Value;
                await SetAsyncInternal(item.Key, data, cacheTime, cancellationToken);
                yield return data;
            }
        }
    }
    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            if (await Redis.KeyExistsAsync(item.Key))
            {
                var results = await GetAsyncInternal<T>(item.Key, cancellationToken);
                yield return results;
            }
            else
            {
                var data = await item.Value;
                await SetAsyncInternal(item.Key, data, cacheTime, cancellationToken);
                yield return data;
            }
        }
    }
    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            if (await Redis.KeyExistsAsync(item.Key))
            {
                var results = await GetAsyncInternal<T>(item.Key, cancellationToken);
                yield return results;
            }
            else
            {
                var data = await item.Value();
                await SetAsyncInternal(item.Key, data, cacheTime, cancellationToken);
                yield return data;
            }
        }
    }
    #endregion
}
