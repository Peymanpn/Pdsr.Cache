using Microsoft.Extensions.Caching.Distributed;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Pdsr.Cache;

public class SqlCacheManager : ICacheManager
{
    private readonly IDistributedCache _cache;

    public SqlCacheManager(
        IDistributedCache cache
        )
    {
        _cache = cache;
    }

    public T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null)
    {
        var dbValue = _cache.GetString(key);
        if (dbValue is not null && !string.IsNullOrEmpty(dbValue))
        {
            return Deserialize<T>(dbValue);
        }
        else
        {
            var newValue = acquire();
            _cache.SetString(key, Serialize(newValue));
            return newValue;
        }
    }

    public async Task<T?> GetAsync<T>(string key, Task<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default) => await GetAsync(key: key, acquire: () => acquire, cacheTime: cacheTime, cancellationToken: cancellationToken);

    public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        var dbValue = await _cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(dbValue))
        {
            var newValue = await acquire();

            await _cache.SetStringAsync(key, Serialize(newValue), GetCacheEntryOptions(cacheTime), cancellationToken);
            return newValue;
        }
        else { return Deserialize<T>(dbValue); }
    }

    public Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
            => GetAsync(key: key, Task.FromResult(acquire()), cacheTime: cacheTime, cancellationToken: cancellationToken);


    public string Get(string key, Func<string> acquire, int? cacheTime = null)
    {
        var dbValue = _cache.GetString(key);
        if (dbValue is null || string.IsNullOrEmpty(dbValue))
        {
            var newValue = acquire();
            _cache.SetString(key, newValue, GetCacheEntryOptions(cacheTime));
            return newValue;
        }
        else { return dbValue; }
    }

    public int Get(string key, Func<int> acquire, int? cacheTime = null)
    {
        var dbValue = _cache.GetString(key);
        if (string.IsNullOrEmpty(dbValue))
        {
            var newValue = acquire();
            _cache.SetString(key, newValue.ToString(), GetCacheEntryOptions(cacheTime));
            return newValue;
        }
        else { return int.Parse(dbValue); }
    }

    public long Get(string key, Func<long> acquire, int? cacheTime = null)
    {
        var dbValue = _cache.GetString(key);
        if (string.IsNullOrEmpty(dbValue))
        {
            var newValue = acquire();
            _cache.SetString(key, newValue.ToString(), GetCacheEntryOptions(cacheTime));
            return newValue;
        }
        else { return long.Parse(dbValue); }
    }

    public byte[] Get(string key, Func<byte[]> acquire, int? cacheTime = null)
    {
        var dbValue = _cache.Get(key);
        if (dbValue == null)
        {
            var newValue = acquire();
            _cache.Set(key, newValue, GetCacheEntryOptions(cacheTime));
            return newValue;
        }
        else { return dbValue; }
    }

    public T? Get<T>(string key)
    {
        var dbVal = _cache.GetString(key);
        if (string.IsNullOrEmpty(dbVal)) return default;
        else return Deserialize<T>(dbVal);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var dbVal = await _cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(dbVal)) return default;
        else return Deserialize<T>(dbVal);
    }

    //public Task<T> GetAsync<T>(string key, bool demandReplica = true, CancellationToken cancellationToken = default)
    //{
    //    return GetAsync<T>(key, cancellationToken: cancellationToken);
    //}

    public async Task<string?> GetAsync(string key, Func<Task<string?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        return await GetAsync<string?>(key, acquire, cacheTime, cancellationToken);
    }

    public async Task<int?> GetAsync(string key, Func<Task<int?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        return await GetAsync<int?>(key, acquire, cacheTime, cancellationToken);
    }

    public async Task<long?> GetAsync(string key, Func<Task<long?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        return await GetAsync<long?>(key, acquire, cacheTime, cancellationToken);
    }

    public async Task<byte[]?> GetAsync(string key, Func<Task<byte[]?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        byte[]? dbValue = await _cache.GetAsync(key, cancellationToken);
        if (dbValue == null)
        {
            var newValue = await acquire();
            if (newValue is not null)
            {
                await _cache.SetAsync(key, newValue, GetCacheEntryOptions(cacheTime), cancellationToken);
            }
            return newValue;
        }
        else { return dbValue; }
    }

    //public async Task SetAsync(string key, object data, int? cacheTime = null)
    //{

    //    await _cache.SetStringAsync(
    //        key,
    //        Serialize(data),
    //           GetCacheEntryOptions(cacheTime)
    //        );

    //}
    public void Set(string key, object data, int? cacheTime = null)
    {
        _cache.SetString(key, Serialize(data), GetCacheEntryOptions(cacheTime));
    }
    public void Set(string key, object data, TimeSpan? expiry = null)
    {
        _cache.SetString(key, Serialize(data), GetCacheEntryOptions(expiry));
    }

    public void Set(string key, byte[] data, int? cacheTime = null)
    {
        _cache.SetString(key, Serialize(data), GetCacheEntryOptions(cacheTime));
    }

    public void Set(string key, byte[] data, TimeSpan? expiry = null)
    {
        _cache.SetString(key, Serialize(data), GetCacheEntryOptions(expiry));
    }

    public async Task SetAsync(string key, object data, TimeSpan? expiry = null, CancellationToken cancellation = default)
    {
        await _cache.SetStringAsync(key, Serialize(data), GetCacheEntryOptions(expiry), cancellation);
    }

    public async Task SetAsync(string key, object data, int? cacheTime = null, CancellationToken cancellation = default)
    {
        await _cache.SetStringAsync(key, Serialize(data), GetCacheEntryOptions(cacheTime), cancellation);
    }

    public Task SetAsync(string key, byte[] data, int? cacheTime = null, CancellationToken cancellation = default)
    {
        return _cache.SetStringAsync(key, Serialize(data), GetCacheEntryOptions(cacheTime), cancellation);
    }

    //public async Task SetAsync(string key, byte[] data, TimeSpan? cacheTime = null, CancellationToken cancellation = default)
    //{
    //    await _cache.SetAsync(key, data, GetCacheEntryOptions(cacheTime), cancellation);
    //}

    public bool IsSet(string key)
    {
        return _cache.Get(key) != null;
    }

    public async Task<bool> IsSetAsync(string key, CancellationToken cancellation = default)
    {
        return await _cache.GetAsync(key, cancellation) != null;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellation = default)
    {
        await _cache.RemoveAsync(key, cancellation);
    }

    public void RemoveByPattern(string pattern)
    {
        throw new NotImplementedException();
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellation = default)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public Task ClearAsync(CancellationToken cancellation = default)
    {
        throw new NotImplementedException();
    }












    #region Utilities

    private DistributedCacheEntryOptions GetCacheEntryOptions(int? cacheTime = null)
    {
        if (cacheTime.HasValue)
            return new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheTime.Value) };
        else
            return new DistributedCacheEntryOptions();
    }
    private DistributedCacheEntryOptions GetCacheEntryOptions(TimeSpan? expiry = null)
    {
        return new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry };
    }

    private string Serialize<T>(T data)
    {
        //JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        //{
        //    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        //};
        // return JsonConvert.SerializeObject(data, serializerSettings);
        return JsonSerializer.Serialize(data);
    }

    private T? Deserialize<T>(string? value) => value is null ? default : JsonSerializer.Deserialize<T>(value);


    public void Dispose()
    {
        // throw new NotImplementedException();
    }

    public Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken) => Task.FromResult<long>(-1);

    public Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<TimeSpan?>(null);

    public long GetItemTimeToLive(string key) => -1;

    public TimeSpan? GetItemTimeSpanToLive(string key) => null;

    public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.CompletedTask;
        return _cache.SetStringAsync(key, Serialize(data), GetCacheEntryOptions(cacheTime), cancellationToken);
    }

    public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default)
    {
        if (data is null) return Task.CompletedTask;
        return _cache.SetStringAsync(key, Serialize(data), GetCacheEntryOptions(expiry), cancellationToken);
    }

    public void Set<T>(string key, T? data, int? cacheTime = null)
    {
        if (data is null) return;
        _cache.SetString(key, Serialize(data), GetCacheEntryOptions(cacheTime));
    }

    public void Set<T>(string key, T? data, TimeSpan? expiry = null)
    {
        _cache.SetString(key, Serialize(data), GetCacheEntryOptions(expiry));
    }

    private Task SetInternalAsync<T>(string key, T data, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        TimeSpan? expiry = null;
        if (cacheTime is not null)
        {
            expiry = TimeSpan.FromSeconds(cacheTime.Value);
        }
        return SetInternalAsync<T>(key, data, expiry, cancellationToken);
    }

    private async Task SetInternalAsync<T>(string key, T data, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        byte[]? bytes;
        if (data is byte[] dataBytes)
        {
            bytes = dataBytes;
        }
        else
        {
            var serialized = Serialize(data);
            bytes = Encoding.UTF8.GetBytes(serialized);
        }
        await _cache.SetAsync(key, bytes, GetCacheEntryOptions(expiry), cancellationToken);
    }
    private async Task<T?> GetInternalAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes is null) return default;
        if (typeof(T) == typeof(byte[])) return (T)(object)bytes;

        var serializedValue = Encoding.UTF8.GetString(bytes);
        var deserialized = Deserialize<T>(serializedValue);
        return deserialized;
    }

    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            var results = await GetInternalAsync<T>(item.Key, cancellationToken: cancellationToken);
            if (results is null)
            {
                var value = await item.Value();
                await SetAsync(item.Key, value, cancellationToken: cancellationToken);
                yield return value;
            }
            else
            {
                await _cache.RefreshAsync(item.Key, cancellationToken);
                yield return results;
            }
        }
    }

    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            var results = await GetInternalAsync<T>(item.Key, cancellationToken: cancellationToken);
            if (results is null)
            {
                var value = await item.Value;
                await SetAsync(item.Key, value, cancellationToken: cancellationToken);
                yield return value;
            }
            else
            {
                await _cache.RefreshAsync(item.Key, cancellationToken);
                yield return results;
            }
        }
    }

    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            var results = await GetInternalAsync<T>(item.Key, cancellationToken: cancellationToken);
            if (results is null)
            {
                await SetAsync(item.Key, item.Value, cancellationToken: cancellationToken);
                yield return item.Value;
            }
            else
            {
                await _cache.RefreshAsync(item.Key, cancellationToken);
                yield return results;
            }
        }
    }

    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            var data = await item.Value();
            if (data is not null)
            {
                await SetInternalAsync<T>(item.Key, data, cacheTime: cacheTime, cancellationToken: cancellationToken);
            }
        }
    }

    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            var data = await item.Value;
            if (data is not null)
            {
                await SetInternalAsync<T>(item.Key, data, cacheTime: cacheTime, cancellationToken: cancellationToken);
            }
        }
    }

    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            var data = item.Value;
            if (data is not null)
            {
                await SetInternalAsync<T>(item.Key, data, cacheTime: cacheTime, cancellationToken: cancellationToken);
            }
        }
    }

    #endregion
}
