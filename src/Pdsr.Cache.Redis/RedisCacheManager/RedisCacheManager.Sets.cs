namespace Pdsr.Cache;

public partial class RedisCacheManager
{
    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetSetAsync(string setKeyName, CancellationToken cancellationToken = default)
    {
        var members = await ExecuteAsync(db => db.SetMembersAsync(setKeyName), cancellationToken).ConfigureAwait(false);
        return members.Select(m => m.ToString()).ToList();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T?>> GetSetItemsAsync<T>(string setKeyName, string? cacheItemKeyPrefix = null, CancellationToken cancellationToken = default)
    {
        var members = await ExecuteAsync(db => db.SetMembersAsync(setKeyName), cancellationToken).ConfigureAwait(false);
        var reads = members.Select(m => TryGetAsync<T>(cacheItemKeyPrefix + m, cancellationToken));
        var results = await Task.WhenAll(reads).ConfigureAwait(false);
        return results.Where(r => r.HasValue).Select(r => r.Value).ToList();
    }

    /// <inheritdoc/>
    public async Task AddToSetAsync<T>(string setKeyName, string key, T value, string? cacheItemKeyPrefix = null, int? cacheTime = null, CancellationToken cancellationToken = default)
    {
        await SetAsync(cacheItemKeyPrefix + key, value, cacheTime, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(db => db.SetAddAsync(setKeyName, key), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveFromSetAsync(string setKeyName, string key, string? cacheItemKeyPrefix = null, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(db => db.SetRemoveAsync(setKeyName, key), cancellationToken).ConfigureAwait(false);
        await RemoveAsync(cacheItemKeyPrefix + key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<long> GetSetLengthAsync(string setKeyName, CancellationToken cancellationToken = default)
        => ExecuteAsync(db => db.SetLengthAsync(setKeyName), cancellationToken);
}
