using Pdsr.Cache.Internal;

namespace Pdsr.Cache;

public partial class RedisCacheManager
{
    #region Internal

    /// <summary>
    /// The single <c>SET</c> every synchronous write goes through. Not called for null values or non-positive expiries.
    /// </summary>
    protected virtual void SetCore<T>(string key, T data, TimeSpan? expiry)
        => Redis.StringSet(key, Serialize(data), expiry, When.Always);

    /// <summary>
    /// The single <c>SET</c> every asynchronous write goes through. Not called for null values or non-positive expiries.
    /// </summary>
    protected virtual Task SetCoreAsync<T>(string key, T data, TimeSpan? expiry, CancellationToken cancellationToken)
        => ExecuteAsync(db => db.StringSetAsync(key, Serialize(data), expiry, When.Always), cancellationToken);

    /// <summary>
    /// Converts a cache time in seconds to an expiry. Returns false when the value shouldn't be cached (zero or negative).
    /// </summary>
    private static bool TryGetExpiry(int? seconds, out TimeSpan? expiry)
    {
        expiry = seconds is null ? null : TimeSpan.FromSeconds(seconds.Value);
        return IsCacheable(expiry);
    }

    private static bool IsCacheable(TimeSpan? expiry) => expiry is null || expiry.Value > TimeSpan.Zero;

    #endregion

    #region Set Synchronous

    ///<inheritdoc/>
    public void Set<T>(string key, T? data, int? cacheTime = null)
    {
        if (data is not null && TryGetExpiry(cacheTime, out var expiry))
            SetCore(key, data, expiry);
    }

    ///<inheritdoc/>
    public void Set<T>(string key, T? data, TimeSpan? expiry)
    {
        if (data is not null && IsCacheable(expiry))
            SetCore(key, data, expiry);
    }

    #endregion

    #region Set Asynchronous

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return data is not null && TryGetExpiry(cacheTime, out var expiry)
            ? SetCoreAsync(key, data, expiry, cancellationToken)
            : Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return data is not null && IsCacheable(expiry)
            ? SetCoreAsync(key, data, expiry, cancellationToken)
            : Task.CompletedTask;
    }

    ///<inheritdoc/>
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        if (acquireKeyPair is null) throw new ArgumentNullException(nameof(acquireKeyPair));
        if (!TryGetExpiry(cacheTime, out var expiry)) return;

        var writes = new List<Task>();
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var value = await item.Value().ConfigureAwait(false);
            if (value is not null) writes.Add(SetCoreAsync(item.Key, value, expiry, cancellationToken));
        }
        await Task.WhenAll(writes).WithCancellation(cancellationToken).ConfigureAwait(false);
    }

    ///<inheritdoc/>
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        if (acquireKeyPair is null) throw new ArgumentNullException(nameof(acquireKeyPair));
        if (!TryGetExpiry(cacheTime, out var expiry)) return;

        var writes = new List<Task>();
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var value = await item.Value.ConfigureAwait(false);
            if (value is not null) writes.Add(SetCoreAsync(item.Key, value, expiry, cancellationToken));
        }
        await Task.WhenAll(writes).WithCancellation(cancellationToken).ConfigureAwait(false);
    }

    ///<inheritdoc/>
    public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
    {
        if (acquireKeyPair is null) throw new ArgumentNullException(nameof(acquireKeyPair));
        if (!TryGetExpiry(cacheTime, out var expiry)) return;

        var writes = new List<Task>();
        await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (item.Value is not null) writes.Add(SetCoreAsync(item.Key, item.Value, expiry, cancellationToken));
        }
        await Task.WhenAll(writes).WithCancellation(cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
