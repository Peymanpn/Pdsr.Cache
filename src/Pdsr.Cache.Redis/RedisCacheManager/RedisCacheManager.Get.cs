using System.Runtime.CompilerServices;

namespace Pdsr.Cache;

public partial class RedisCacheManager
{
    #region Single read

    /// <inheritdoc/>
    public bool TryGet<T>(string key, out T? value)
    {
        var result = TryGetCore<T>(key, CommandFlags.None);
        value = result.Value;
        return result.HasValue;
    }

    /// <inheritdoc/>
    public Task<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
        => TryGetCoreAsync<T>(key, CommandFlags.None, cancellationToken);

    /// <summary>
    /// The single <c>GET</c> every synchronous read goes through.
    /// </summary>
    protected virtual CacheResult<T> TryGetCore<T>(string key, CommandFlags flags)
        => ToResult<T>(Redis.StringGet(key, flags));

    /// <summary>
    /// The single <c>GET</c> every asynchronous read goes through.
    /// </summary>
    protected virtual async Task<CacheResult<T>> TryGetCoreAsync<T>(string key, CommandFlags flags, CancellationToken cancellationToken)
        => ToResult<T>(await ExecuteAsync(db => db.StringGetAsync(key, flags), cancellationToken).ConfigureAwait(false));

    private static CacheResult<T> ToResult<T>(RedisValue value)
        => value.IsNull ? CacheResult<T>.Miss : CacheResult<T>.Hit(Deserialize<T>(value));

    private static CommandFlags ReadFlags(bool preferReplica) => preferReplica ? CommandFlags.PreferReplica : CommandFlags.None;

    #endregion

    #region Get Synchronous

    ///<inheritdoc/>
    public T? Get<T>(string key) => TryGetCore<T>(key, CommandFlags.None).Value;

    ///<inheritdoc/>
    public T? Get<T>(string key, bool preferReplica) => TryGetCore<T>(key, ReadFlags(preferReplica)).Value;

    ///<inheritdoc/>
    public T? Get<T>(string key, Func<T?> acquire, int? cacheTime = null)
    {
        if (acquire is null) throw new ArgumentNullException(nameof(acquire));

        CacheResult<T> cached;
        try
        {
            cached = TryGetCore<T>(key, CommandFlags.None);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            return acquire();
        }
        if (cached.HasValue && cached.Value is not null) return cached.Value;

        var value = acquire();
        try
        {
            Set(key, value, cacheTime);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            // The value is still correct; it just isn't cached this time.
        }
        return value;
    }

    #endregion

    #region Get Asynchronous

    ///<inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => (await TryGetAsync<T>(key, cancellationToken).ConfigureAwait(false)).Value;

    ///<inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, bool preferReplica, CancellationToken cancellationToken = default)
        => (await TryGetCoreAsync<T>(key, ReadFlags(preferReplica), cancellationToken).ConfigureAwait(false)).Value;

    ///<inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        if (acquire is null) throw new ArgumentNullException(nameof(acquire));

        CacheResult<T> cached;
        try
        {
            cached = await TryGetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            return await acquire().ConfigureAwait(false);
        }
        if (cached.HasValue && cached.Value is not null) return cached.Value;

        var value = await acquire().ConfigureAwait(false);
        try
        {
            await SetAsync(key, value, cacheTime, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            // The value is still correct; it just isn't cached this time.
        }
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

    #endregion
}
