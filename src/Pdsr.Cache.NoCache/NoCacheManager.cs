using System.Runtime.CompilerServices;

namespace Pdsr.Cache;

/// <summary>
/// An <see cref="ICacheManager"/> that caches nothing: every read misses and read-through calls always acquire.
/// </summary>
public class NoCacheManager : ICacheManager
{
    /// <inheritdoc/>
    public T? Get<T>(string key) => default;

    /// <inheritdoc/>
    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        return false;
    }

    /// <inheritdoc/>
    public T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null)
        => (acquire ?? throw new ArgumentNullException(nameof(acquire)))();

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);

    /// <inheritdoc/>
    public Task<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(CacheResult<T>.Miss);

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        => (acquire ?? throw new ArgumentNullException(nameof(acquire)))();

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string key, Task<T?> acquireTask, int? cacheTime = null, CancellationToken cancellationToken = default)
        => acquireTask ?? throw new ArgumentNullException(nameof(acquireTask));

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        => Task.FromResult((acquire ?? throw new ArgumentNullException(nameof(acquire)))());

    /// <inheritdoc/>
    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return await item.Value().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return await item.Value.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return item.Value;
    }

    /// <inheritdoc/>
    public void Set<T>(string key, T? data, int? cacheTime = null) { }

    /// <inheritdoc/>
    public void Set<T>(string key, T? data, TimeSpan? expiry) { }

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public bool IsSet(string key) => false;

    /// <inheritdoc/>
    public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc/>
    public void Remove(string key) { }

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public void RemoveByPattern(string pattern) { }

    /// <inheritdoc/>
    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public void Clear() { }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public TimeSpan? GetItemTimeSpanToLive(string key) => null;

    /// <inheritdoc/>
    public long GetItemTimeToLive(string key) => -1L;

    /// <inheritdoc/>
    public Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<TimeSpan?>(null);

    /// <inheritdoc/>
    public Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(-1L);

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);
}
