using Pdsr.Cache.Configurations;

namespace Pdsr.Cache;

public partial class RedisCacheManager : ICacheManager, IRedisCacheManager
{
    private readonly IRedisConnectionFactory _redisConnectionFactory;

    public RedisCacheManager(
        IRedisConfiguration redisConfigurationOptions,
        IRedisConnectionFactory redisConnectionFactory
        )
    {
        _redisConnectionFactory = redisConnectionFactory;
        _redisConfiguration = redisConfigurationOptions;
    }

    private readonly IRedisConfiguration _redisConfiguration;

    ///<inheritdoc/> 
    public IDatabase Redis => _redisConnectionFactory.Connection().GetDatabase();

    /// <inheritdoc/>
    public bool IsSet(string key) => Redis.IsConnected(key) && Redis.KeyExists(key);

    /// <inheritdoc/>
    public async Task<bool> IsSetAsync(string key, CancellationToken cancellation = default) => Redis.IsConnected(key) && await Redis.KeyExistsAsync(key);

    /// <inheritdoc/>
    public void Remove(string key)
    {
        Redis.KeyDelete(key);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellation = default)
    {
        await Redis.KeyDeleteAsync(key);
    }

    /// <inheritdoc/>
    public void RemoveByPattern(string pattern)
    {
        var keys = Server.Keys(pattern: pattern);
        Redis.KeyDelete(keys.ToArray());
    }

    /// <inheritdoc/>
    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellation = default)
    {
        //do
        //{
        var keys = Server.KeysAsync(pattern: pattern, pageSize: 1);

        await foreach (var key in keys)
        {
            await Redis.KeyDeleteAsync(key);
        }
        //} while (Server.Keys(pattern: pattern).Any());
    }

    /// <inheritdoc/>
    public void Clear() => Server.FlushDatabase();

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellation = default) => Server.FlushDatabaseAsync();

    /// <inheritdoc/>
    public TimeSpan? GetItemTimeSpanToLive(string key) => Redis.KeyTimeToLive(key);
    /// <inheritdoc/>
    public long GetItemTimeToLive(string key)
    {
        var ttl = Redis.KeyTimeToLive(key);
        return ttl.HasValue ? (long)ttl.Value.TotalSeconds : -1;
    }

    /// <inheritdoc/>
    public Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default)
        => Redis.KeyTimeToLiveAsync(key);


    /// <inheritdoc/>
    public async Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        var ttl = await Redis.KeyTimeToLiveAsync(key);
        return ttl.HasValue ? (long)ttl.Value.TotalSeconds : -1;
    }

    // Set related methods
    #region Set

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetSetAsync(string setKeyName, CancellationToken cancellationToken = default)
    {
        var smemebers = await Redis.SetMembersAsync(setKeyName);
        return smemebers.Select(c => c.ToString());
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T?>> GetSetItemsAsync<T>(string setKeyName, string? cacheItemKeyPrefix = null, CancellationToken cancellationToken = default)
    {
        var smemebers = await Redis.SetMembersAsync(setKeyName);
        List<string> keyList = smemebers.Select(c => cacheItemKeyPrefix + c).ToList();
        List<T?> objects = new List<T?>(keyList.Count);
        for (int i = 0; i < keyList.Count; i++)
        {
            objects[i] = await GetAsync<T>(keyList[i], cancellationToken: cancellationToken);
        }
        return objects;
    }

    /// <inheritdoc/>
    public async Task AddToSetAsync<T>(string setKeyName, string key, T value, string? cacheItemKeyPrefix = null, int? cacheTime = null, CancellationToken cancellationToken = default)

    {
        await Redis.SetAddAsync(setKeyName, key);
        await SetAsync(cacheItemKeyPrefix + key, value, cacheTime, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RemoveFromSetItemAsync(string setKeyName, string keyName, CancellationToken cancellationToken = default)
    {
        return Redis.SetRemoveAsync(setKeyName, keyName);
    }

    /// <inheritdoc/>
    public Task<long> GetSetLength(string setKeyName, CancellationToken cancellationToken = default)
    {
        return Redis.SetLengthAsync(setKeyName);
    }

    #endregion


    #region IDisposable Support

    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _redisConnectionFactory.Connection().Dispose();
                // TODO: dispose managed state (managed objects).
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            disposedValue = true;
        }
    }

    // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
    // ~SqlCacheManager() {
    //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
    //   Dispose(false);
    // }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        // TODO: uncomment the following line if the finalizer is overridden above.
        // GC.SuppressFinalize(this);
    }
    #endregion

    #region Utilities

    // TODO: use System.Text.Json instead of Newtonsoft

    /// <summary>
    /// Serialize using Newtonsoft Json.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <returns></returns>
    // private string Serialize<T>(T data) => JsonConvert.SerializeObject(data);
    private string Serialize<T>(T data) => System.Text.Json.JsonSerializer.Serialize<T>(data);

    // private T Deserialize<T>(string value) => JsonConvert.DeserializeObject<T>(value);
    private T? Deserialize<T>(string value) => System.Text.Json.JsonSerializer.Deserialize<T>(value);

    #endregion

    /// <inheritdoc/>
    public IServer Server
    {
        get
        {
            if (!string.IsNullOrEmpty(_redisConfiguration.Host) && _redisConfiguration.Port > 0)
                return _redisConnectionFactory.Connection().GetServer($"{_redisConfiguration.Host}:{_redisConfiguration.Port}");
            else if (_redisConfiguration.EndPoints != null && !string.IsNullOrEmpty(_redisConfiguration.EndPoints[0]))
                return _redisConnectionFactory.Connection().GetServer(_redisConnectionFactory.Connection().GetEndPoints().ToList()[0]);
            else throw new InvalidOperationException();
        }
    }


    /// <inheritdoc/>
    public ISubscriber Subscriber { get => _redisConnectionFactory.Connection().GetSubscriber(); }

    /// <inheritdoc/>
    public IEnumerable<string> Keys => Server.Keys().Select(c => c.ToString());

    /// <inheritdoc/>
    public IEnumerable<string> GetKeysForUser(string subjectId)
    {
        return GetKeysForUserByPrefix(subjectId);
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetKeysForUserByPrefix(string subjectId, string prefix = "????:")
    {
        return Server.Keys(pattern: $"{prefix}{subjectId}:*").Select(c => c.ToString());
    }
}
