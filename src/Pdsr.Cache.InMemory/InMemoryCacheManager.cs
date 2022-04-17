using Pdsr.Cache.InMemory;
using Pdsr.Cache.InMemory.Configurations;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Pdsr.Cache
{
    public class InMemoryCacheManager : ICacheManager
    {
        private readonly CachedData _cache;
        private readonly InMemoryCacheConfig _inMemoryCacheConfig;

        internal InMemoryCacheManager(CachedData cache, InMemoryCacheConfig inMemoryCacheConfig)
        {
            _cache = cache;
            _inMemoryCacheConfig = inMemoryCacheConfig;
        }


        private bool TryGetData<T>(string key, out T? data)
        {
            // var d = _cache.Data.TryGetValue(key, out object value);
            var d = _cache.TryGetValue(key, out CacheDataItem? value);
            if (d && value is not null && !IsExpired(value.expiry))
            {
                data = (T?)value.data;
                // EvictExpiredKeys();
                return true;
            }
            EvictExpiredKeys();
            data = default;
            return false;
        }

        private void EvictExpiredKeys()
        {
            //var lst = _cache.Keys;//.ToList();
            //lst.ForEach(kv =>
            //{
            //    if (_cache.ContainsKey(kv)&& IsExpired(_cache[kv].expiry))
            //    {
            //        _cache.Remove(kv);
            //    }
            //});
            object lstLock = new object();
            //Monitor.Enter(lstLock);
            lock (_cache)
            {
                string[] keys = new string[_cache.Keys.Count];
                _cache.Keys.CopyTo(keys, 0);
                foreach (var item in keys)
                {
                    if (_cache.ContainsKey(item) && IsExpired(_cache[item].expiry))
                    {
                        _cache.Remove(item);
                    }
                }
            }
            //Monitor.Exit(lstLock);
        }

        private void EvictExpiredKeysBg()
        {
            Task evictTask = Task.Run(() =>
              {
                  EvictExpiredKeys();
              });
            if (evictTask.Status == TaskStatus.WaitingForActivation)
                evictTask.Start();

        }


        private void SetData<T>(string key, T? data, int? cacheTime = null)
        {
            if (data is null) return;
            if (_cache.Count > _inMemoryCacheConfig.MaxEnteriesCount)
                throw new Exception($"Cache entries exceeded the max count {_inMemoryCacheConfig.MaxEnteriesCount}");
            if (cacheTime is null)
            {
                // SetData<T>(key, data,cacheTime);
                _cache.Add(key,
                    //(DateTimeOffset.MaxValue, data)
                    new CacheDataItem(DateTimeOffset.MaxValue, data)
                    );
            }
            else
            {
                _cache.Add(key,
                    // (GetExpireDate(cacheTime.Value), data)
                    new CacheDataItem(GetExpireDate(cacheTime.Value), data)
                    );
            }
        }

        private DateTimeOffset GetExpireDate(int cacheTime) => DateTimeOffset.UtcNow.AddSeconds(cacheTime);

        private bool IsExpired(DateTimeOffset date) => DateTimeOffset.UtcNow > date;

        public T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null)
        {
            if (TryGetData<T>(key, out var data))
            {
                return data;
            }
            else
            {
                SetData<T>(key, acquire(), cacheTime);
                return Get<T>(key, acquire, cacheTime);
            }
        }

        public T? Get<T>(string key)
        {
            TryGetData(key, out T? value);
            return value;
        }

        public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            if (TryGetData<T>(key, out var data))
            {
                return data;
            }
            else
            {
                SetData<T>(key, await acquire(), cacheTime);
                return await GetAsync<T>(key, acquire, cacheTime, cancellationToken);
            }
        }

        public Task<T?> GetAsync<T>(string key, Task<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            return GetAsync<T>(key, () => acquire, cacheTime, cancellationToken);
        }

        public Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            var hasData = TryGetData<T>(key, out T? data);
            if (hasData) return Task.FromResult(data);
            else
            {
                T? results = acquire();
                if (results is null) return Task.FromResult<T?>(default);
                SetData<T>(key, results, cacheTime);
                return Task.FromResult<T?>(results);
            }
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            TryGetData<T>(key, out T? value);
            return Task.FromResult(value);
        }

        public void Set(string key, object data, int? cacheTime = null)
        {
            SetData(key, data, cacheTime);
        }

        public void Set(string key, byte[] data, int? cacheTime = null)
        {
            SetData(key, data, cacheTime);
        }

        public Task SetAsync(string key, object data, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            SetData(key, data, cacheTime);
            return Task.CompletedTask;
        }

        public Task SetAsync(string key, byte[] data, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            SetData(key, data, cacheTime);
            return Task.CompletedTask;
        }

        public bool IsSet(string key) => _cache.TryGetValue(key, out var data) && !IsExpired(data.expiry);

        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_cache.TryGetValue(key, out var data) && !IsExpired(data.expiry));

        public void Remove(string key)
        {
            _cache.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        public void RemoveByPattern(string pattern)
        {
            var keys = _cache.Keys.Where(k => Regex.IsMatch(k, pattern));
            foreach (var key in keys) Remove(key);
        }

        public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            RemoveByPattern(pattern);
            return Task.CompletedTask;
        }

        public void Clear() => _cache.Clear();

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _cache.Clear();
            return Task.CompletedTask;
        }

        public Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(GetItemTimeToLive(key));

        public Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(GetItemTimeSpanToLive(key));

        public long GetItemTimeToLive(string key)
        {
            if (_cache.TryGetValue(key, out var data))
            {
                long ttl = (long)data.expiry.Subtract(DateTimeOffset.UtcNow).TotalSeconds;
                return ttl;
            }
            return -1L;
        }

        public TimeSpan? GetItemTimeSpanToLive(string key)
            => (TimeSpan?)TimeSpan.FromSeconds(GetItemTimeToLive(key));



        public void Dispose() => _cache.Clear();

        public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            if (data is not null)
            {
                SetData(key, data, cacheTime);
            }
            return Task.CompletedTask;
        }

        public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default)
        {
            SetData(key, data, (int?)expiry?.TotalSeconds);
            return Task.CompletedTask;
        }

        public void Set<T>(string key, T? value, int? cacheTime = null)
        {
            SetData(key, value, cacheTime);
        }

        public void Set<T>(string key, T? value, TimeSpan? expiry = null)
        {
            SetData(key, value, (int?)expiry?.TotalSeconds);
        }

        public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in acquireKeyPair)
            {
                if (TryGetData<T>(item.Key, out var data))
                {
                    yield return data;
                }
                else
                {
                    var results = await item.Value();
                    SetData<T>(item.Key, results, cacheTime: cacheTime);
                    yield return results;
                }
            }
        }

        public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in acquireKeyPair)
            {
                if (TryGetData<T>(item.Key, out var data))
                {
                    yield return data;
                }
                else
                {
                    var results = await item.Value;
                    SetData<T>(item.Key, results, cacheTime: cacheTime);
                    yield return results;
                }
            }
        }

        public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in acquireKeyPair)
            {
                if (TryGetData<T>(item.Key, out var data))
                {
                    yield return data;
                }
                else
                {
                    var results = item.Value;
                    SetData<T>(item.Key, results, cacheTime: cacheTime);
                    yield return results;
                }
            }
        }

        public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
        {
            await foreach (var item in acquireKeyPair)
            {
                SetData<T>(item.Key, await item.Value(), cacheTime);
            }
        }

        public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
        {
            await foreach (var item in acquireKeyPair)
            {
                SetData<T>(item.Key, await item.Value, cacheTime);
            }
        }

        public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
        {
            await foreach (var item in acquireKeyPair)
            {
                SetData<T>(item.Key, item.Value, cacheTime);
            }
        }
    }
}
