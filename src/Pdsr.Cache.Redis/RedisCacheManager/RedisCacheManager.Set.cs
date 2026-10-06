namespace Pdsr.Cache;

public partial class RedisCacheManager
{
    #region Internal
    private Task SetAsyncInternal<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        TimeSpan? expiry = GetExpiry(cacheTime);
        return SetAsyncInternal<T>(key, data, expiry, cancellationToken);
    }

    private Task SetAsyncInternal<T>(string key, T? data, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        if (data is not null)
            return Redis.StringSetAsync(key, Serialize(data), expiry, When.Always);
        else
            return Task.CompletedTask;
    }

    private void SetInternal<T>(string key, T? data, int? cacheTime = null)
    {
        TimeSpan? expiry = GetExpiry(cacheTime);
        SetInternal<T>(key, data, expiry);
    }

    private void SetInternal<T>(string key, T? data, TimeSpan? expiry = null)
    {
        if (data is not null)
            Redis.StringSetAsync(key, Serialize(data), expiry, When.Always);
    }


    private TimeSpan? GetExpiry(int? seconds)
    {
        if (seconds is null)
        {
            return null;
        }
        else
        {
            return TimeSpan.FromSeconds((double)seconds);
        }
    }
    #endregion

    #region Set Synchronous

    ///<inheritdoc/> 
    public void Set<T>(string key, T? data, int? cacheTime = null) => SetInternal(key, data, cacheTime);

    ///<inheritdoc/> 
    public void Set<T>(string key, T? data, TimeSpan? expiry = null) => SetInternal(key, data, expiry);

    #endregion

    #region Set Cache
    /// <inheritdoc/>


    /// <inheritdoc/>
    public Task SetAsync(string key, byte[] data, int? cacheTime = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return SetAsyncInternal(key, data, cacheTime, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual Task SetAsync(string key, object data, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        return SetAsyncInternal(key, data, cacheTime, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        return SetAsyncInternal<T>(key, data, cacheTime, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default)
    {
        return SetAsyncInternal(key, data, expiry, cancellationToken);
    }

    #endregion


    #region IAsync Enums

    ///<inheritdoc/> 
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireTasksKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        // if we don't use timed caching, we can set all together in one go
        if (cacheTime is null)
        {
            var allPairs = new List<KeyValuePair<RedisKey, RedisValue>>();
            await foreach (var a in acquireTasksKeyPair.WithCancellation(cancellationToken))
            {
                var value = await a.Value();
                if (value is not null)
                    allPairs.Add(new KeyValuePair<RedisKey, RedisValue>(a.Key, Serialize(value)));
            }
            await Redis.StringSetAsync(allPairs.ToArray());
        }
        else
        {
            await foreach (var a in acquireTasksKeyPair.WithCancellation(cancellationToken))
            {
                var value = await a.Value();
                if (value is not null)
                    await SetAsyncInternal<T>(a.Key, value, cacheTime, cancellationToken);
            }
        }
    }

    ///<inheritdoc/> 
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        // if we don't use timed caching, we can set all together in one go
        if (cacheTime is null)
        {
            var values = new List<KeyValuePair<RedisKey, RedisValue>>();
            await foreach (var a in acquireKeyPair.WithCancellation(cancellationToken))
            {
                var value = await a.Value;
                if (value is not null)
                    values.Add(new KeyValuePair<RedisKey, RedisValue>(a.Key, Serialize(value)));
            }
            await Redis.StringSetAsync(values.ToArray());
        }
        else
        {
            await foreach (var a in acquireKeyPair.WithCancellation(cancellationToken))
            {
                var value = await a.Value;
                if (value is not null)
                    await SetAsyncInternal<T>(a.Key, value, cacheTime, cancellationToken);
            }
        }
    }

    ///<inheritdoc/> 
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        // if we don't use timed caching, we can set all together in one go
        if (cacheTime is null)
        {
            var values = await acquireKeyPair.Where(a => a.Value is not null)
                    .Select(a => new KeyValuePair<RedisKey, RedisValue>(a.Key, Serialize(a.Value)))
                    .ToArrayAsync(cancellationToken);
            await Redis.StringSetAsync(values);
        }
        else
        {
            await foreach (var a in acquireKeyPair.WithCancellation(cancellationToken))
            {
                if (a.Value is not null)
                    await SetAsyncInternal(a.Key, a.Value, cacheTime, cancellationToken);
            }
        }
    }

    #endregion
}
