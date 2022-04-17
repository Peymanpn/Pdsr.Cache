using System.Runtime.CompilerServices;

namespace Pdsr.Cache;

public class NoCacheManager : ICacheManager
{
    public T? Get<T>(string key) => default;
    public T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null) => acquire();

    public string Get(string key, Func<string> acquire, int? cacheTime = null) => acquire();

    public int Get(string key, Func<int> acquire, int? cacheTime = null) => acquire();

    public long Get(string key, Func<long> acquire, int? cacheTime = null) => acquire();

    public byte[] Get(string key, Func<byte[]> acquire, int? cacheTime = null) => acquire();

    public Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellation = default) => acquire();

    public Task<string> GetAsync(string key, Func<Task<string>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default) => acquire();

    public Task<int> GetAsync(string key, Func<Task<int>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default) => acquire();

    public Task<long> GetAsync(string key, Func<Task<long>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default) => acquire();

    public Task<byte[]> GetAsync(string key, Func<Task<byte[]>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default) => acquire();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
    public Task<T?> GetAsync<T>(string key, Task<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default) => acquire;
    public Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default) => Task.FromResult(acquire());

    public void Set(string key, object data, int? cacheTime = null) { }

    public void Set(string key, object data, TimeSpan? expiry = null) { }

    public void Set(string key, byte[] data, int? cacheTime = null) { }

    public void Set(string key, byte[] data, TimeSpan? expiry = null) { }

    public Task SetAsync(string key, object data, TimeSpan? expiry = null, CancellationToken cancellation = default) => Task.CompletedTask;

    public Task SetAsync(string key, object data, int? cacheTime = null, CancellationToken cancellation = default) => Task.CompletedTask;

    public Task SetAsync(string key, byte[] data, int? cacheTime = null, CancellationToken cancellation = default) => Task.CompletedTask;

    public Task SetAsync(string key, byte[] data, TimeSpan? cacheTime = null, CancellationToken cancellation = default) => Task.CompletedTask;

    public bool IsSet(string key) => true;

    public Task<bool> IsSetAsync(string key, CancellationToken cancellation = default) => Task.FromResult(false);

    public void Remove(string key) { }

    public Task RemoveAsync(string key, CancellationToken cancellation = default) => Task.CompletedTask;

    public void RemoveByPattern(string pattern) { }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellation = default) => Task.CompletedTask;

    public void Clear() { }

    public Task ClearAsync(CancellationToken cancellation = default) => Task.CompletedTask;



    public void Dispose() { }



    public Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<TimeSpan?>(null);



    public TimeSpan? GetItemTimeSpanToLive(string key) => null;
    public Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(-1L);

    public long GetItemTimeToLive(string key) => -1L;

    public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Set<T>(string key, T? value, int? cacheTime = null)
    {

    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {

    }

    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            yield return await item.Value();
        }
    }

    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            yield return await item.Value;
        }
    }

    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair)
        {
            yield return item.Value;
        }
    }

    public Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
