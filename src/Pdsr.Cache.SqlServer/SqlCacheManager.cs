using Microsoft.Extensions.Caching.Distributed;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Pdsr.Cache;

/// <summary>
/// <see cref="ICacheManager"/> over an <see cref="IDistributedCache"/>, typically SQL Server. Values are stored as JSON.
/// </summary>
/// <remarks>
/// <see cref="IDistributedCache"/> can't enumerate keys or report expiry, so pattern removal and
/// clearing throw <see cref="NotSupportedException"/>, and time-to-live reads return -1/null.
/// Items cached without an expiry use the store's default sliding expiration.
/// </remarks>
public class SqlCacheManager : ICacheManager
{
    private readonly IDistributedCache _cache;

    /// <summary>
    /// Creates the cache manager.
    /// </summary>
    public SqlCacheManager(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    #region Reads

    /// <inheritdoc/>
    public bool TryGet<T>(string key, out T? value)
    {
        var stored = _cache.GetString(key);
        if (stored is null)
        {
            value = default;
            return false;
        }
        value = JsonSerializer.Deserialize<T>(stored);
        return true;
    }

    /// <inheritdoc/>
    public async Task<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stored = await _cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        return stored is null ? CacheResult<T>.Miss : CacheResult<T>.Hit(JsonSerializer.Deserialize<T>(stored));
    }

    /// <inheritdoc/>
    public T? Get<T>(string key) => TryGet<T>(key, out var value) ? value : default;

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => (await TryGetAsync<T>(key, cancellationToken).ConfigureAwait(false)).Value;

    /// <inheritdoc/>
    public T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null)
    {
        if (acquire is null) throw new ArgumentNullException(nameof(acquire));
        if (TryGet<T>(key, out var cached) && cached is not null) return cached;

        var value = acquire();
        Set(key, value, cacheTime);
        return value;
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        if (acquire is null) throw new ArgumentNullException(nameof(acquire));
        var cached = await TryGetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (cached.HasValue && cached.Value is not null) return cached.Value;

        var value = await acquire().ConfigureAwait(false);
        await SetAsync(key, value, cacheTime, cancellationToken).ConfigureAwait(false);
        return value;
    }

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string key, Task<T?> acquireTask, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        if (acquireTask is null) throw new ArgumentNullException(nameof(acquireTask));
        return GetAsync(key, () => acquireTask, cacheTime, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        if (acquire is null) throw new ArgumentNullException(nameof(acquire));
        return GetAsync(key, () => Task.FromResult(acquire()), cacheTime, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return await GetAsync(item.Key, item.Value, cacheTime, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return await GetAsync(item.Key, item.Value, cacheTime, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T?> GetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var value = item.Value;
            yield return await GetAsync(item.Key, () => Task.FromResult(value), cacheTime, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public bool IsSet(string key) => _cache.Get(key) is not null;

    /// <inheritdoc/>
    public async Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false) is not null;
    }

    #endregion

    #region Writes

    /// <inheritdoc/>
    public void Set<T>(string key, T? data, int? cacheTime = null) => Set(key, data, ToExpiry(cacheTime));

    /// <inheritdoc/>
    public void Set<T>(string key, T? data, TimeSpan? expiry)
    {
        if (TryGetOptions(data, expiry, out var options))
            _cache.SetString(key, JsonSerializer.Serialize(data), options);
    }

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
        => SetAsync(key, data, ToExpiry(cacheTime), cancellationToken);

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return TryGetOptions(data, expiry, out var options)
            ? _cache.SetStringAsync(key, JsonSerializer.Serialize(data), options, cancellationToken)
            : Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
            await SetAsync(item.Key, await item.Value().ConfigureAwait(false), cacheTime, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
            await SetAsync(item.Key, await item.Value.ConfigureAwait(false), cacheTime, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
            await SetAsync(item.Key, item.Value, cacheTime, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Removal

    /// <inheritdoc/>
    public void Remove(string key) => _cache.Remove(key);

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _cache.RemoveAsync(key, cancellationToken);
    }

    /// <summary>Not supported: <see cref="IDistributedCache"/> can't enumerate keys.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void RemoveByPattern(string pattern) => throw KeysNotSupported();

    /// <summary>Not supported: <see cref="IDistributedCache"/> can't enumerate keys.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default) => throw KeysNotSupported();

    /// <summary>Not supported: <see cref="IDistributedCache"/> can't enumerate keys.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void Clear() => throw KeysNotSupported();

    /// <summary>Not supported: <see cref="IDistributedCache"/> can't enumerate keys.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public Task ClearAsync(CancellationToken cancellationToken = default) => throw KeysNotSupported();

    #endregion

    #region Time to live

    /// <summary>Always -1: <see cref="IDistributedCache"/> doesn't expose expiry.</summary>
    public long GetItemTimeToLive(string key) => -1;

    /// <summary>Always null: <see cref="IDistributedCache"/> doesn't expose expiry.</summary>
    public TimeSpan? GetItemTimeSpanToLive(string key) => null;

    /// <summary>Always -1: <see cref="IDistributedCache"/> doesn't expose expiry.</summary>
    public Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(-1L);

    /// <summary>Always null: <see cref="IDistributedCache"/> doesn't expose expiry.</summary>
    public Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<TimeSpan?>(null);

    #endregion

    /// <inheritdoc/>
    public void Dispose() => GC.SuppressFinalize(this);

    private static TimeSpan? ToExpiry(int? cacheTime) => cacheTime is null ? null : TimeSpan.FromSeconds(cacheTime.Value);

    /// <summary>
    /// Returns false when the value shouldn't be cached: null data or a non-positive expiry.
    /// </summary>
    private static bool TryGetOptions<T>(T? data, TimeSpan? expiry, out DistributedCacheEntryOptions options)
    {
        options = new DistributedCacheEntryOptions();
        if (data is null || expiry <= TimeSpan.Zero) return false;
        options.AbsoluteExpirationRelativeToNow = expiry;
        return true;
    }

    private static NotSupportedException KeysNotSupported()
        => new("IDistributedCache can't enumerate keys, so pattern removal and clearing aren't supported.");
}
