namespace Pdsr.Cache;

public partial class RedisCacheManager
{
    #region Internal
    private Task SetAsyncInternal<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        TimeSpan? expiry = null;
        if (cacheTime is not null) expiry = TimeSpan.FromSeconds(cacheTime.Value);
        return SetAsyncInternal<T>(key, data, expiry, cancellationToken);
    }

    private Task SetAsyncInternal<T>(string key, T? data, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        if (data is not null)
            return Redis.StringSetAsync(key, Serialize(data), expiry);
        else
            return Task.CompletedTask;
    }

    private void SetInternal<T>(string key, T? data, int? cacheTime = null)
    {
        TimeSpan? expiry = null;
        SetInternal<T>(key, data, expiry);
    }

    private void SetInternal<T>(string key, T? data, TimeSpan? expiry = null)
    {
        if (data is not null)
            Redis.StringSetAsync(key, Serialize(data), expiry);
    }



    #endregion

    #region Set Synchronous

    public void Set<T>(string key, T? data, int? cacheTime = null) => SetInternal(key, data, cacheTime);

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

    public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        return SetAsyncInternal<T>(key, data, cacheTime, cancellationToken);
    }

    public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default)
    {
        return SetAsyncInternal(key, data, expiry, cancellationToken);
    }

    #endregion


    #region IAsync Enums

    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireTasksKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        //await foreach (var item in acquireTasksKeyPair)
        //{
        //    var data = await item.Value();
        //    if (data is not null)
        //    {
        //        await SetAsyncInternal<T>(item.Key, data, cacheTime, cancellationToken);
        //    }
        //}
        var allPais = await acquireTasksKeyPair
            .SelectAwait(async a => new KeyValuePair<string, T?>(a.Key, await a.Value()))
            .Where(a => a.Value is not null)
            .Select(a => new KeyValuePair<RedisKey, RedisValue>(a.Key, Serialize(a.Value)))
            .ToArrayAsync();
        await Redis.StringSetAsync(allPais);
    }
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        var values = await acquireKeyPair
            .SelectAwait(async a => new KeyValuePair<string, T?>(a.Key, await a.Value))
            .Where(a => a.Value is not null)
            .Select(a => new KeyValuePair<RedisKey, RedisValue>(a.Key, Serialize(a.Value)))
            .ToArrayAsync();
        await Redis.StringSetAsync(values);

    }
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        var values = await acquireKeyPair
            .Where(a => a.Value is not null)
            .Select(a => new KeyValuePair<RedisKey, RedisValue>(a.Key, Serialize(a.Value)))
            .ToArrayAsync();
        await Redis.StringSetAsync(values);
    }

    #endregion
}
