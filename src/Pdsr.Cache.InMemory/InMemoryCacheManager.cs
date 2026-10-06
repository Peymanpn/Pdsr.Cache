using Pdsr.Cache.InMemory.Configurations;
using Pdsr.Cache.InMemory.Internal;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Pdsr.Cache
{
    /// <summary>
    /// <see cref="ICacheManager"/> held in process memory. Values are stored as JSON, so reads return copies.
    /// Patterns use Redis glob syntax.
    /// </summary>
    public class InMemoryCacheManager : ICacheManager
    {
        private readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.Ordinal);
        private readonly InMemoryCacheConfig _config;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Creates an empty cache.
        /// </summary>
        /// <param name="config">Settings</param>
        /// <param name="timeProvider">Clock used for expiry; defaults to the system clock</param>
        public InMemoryCacheManager(InMemoryCacheConfig config, TimeProvider? timeProvider = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Number of entries currently stored, including expired ones not yet evicted.
        /// </summary>
        public int Count => _cache.Count;

        #region Reads

        /// <inheritdoc/>
        public bool TryGet<T>(string key, out T? value)
        {
            if (TryGetLiveEntry(key, out var entry))
            {
                value = JsonSerializer.Deserialize<T>(entry.Data);
                return true;
            }
            value = default;
            return false;
        }

        /// <inheritdoc/>
        public Task<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryGet<T>(key, out var value) ? CacheResult<T>.Hit(value) : CacheResult<T>.Miss);
        }

        /// <inheritdoc/>
        public T? Get<T>(string key) => TryGet<T>(key, out var value) ? value : default;

        /// <inheritdoc/>
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Get<T>(key));
        }

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
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGet<T>(key, out var cached) && cached is not null) return cached;

            var value = await acquire().ConfigureAwait(false);
            Set(key, value, cacheTime);
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
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Get(key, acquire, cacheTime));
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
                yield return Get(item.Key, () => value, cacheTime);
            }
        }

        /// <inheritdoc/>
        public bool IsSet(string key) => TryGetLiveEntry(key, out _);

        /// <inheritdoc/>
        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(IsSet(key));
        }

        #endregion

        #region Writes

        /// <inheritdoc/>
        public void Set<T>(string key, T? data, int? cacheTime = null)
            => Set(key, data, cacheTime is null ? null : TimeSpan.FromSeconds(cacheTime.Value));

        /// <inheritdoc/>
        public void Set<T>(string key, T? data, TimeSpan? expiry)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (data is null) return;
            if (expiry is { } ttl && ttl <= TimeSpan.Zero) return;
            if (!HasRoomFor(key)) return;

            var entry = new Entry(JsonSerializer.Serialize(data), expiry is null ? null : _timeProvider.GetUtcNow() + expiry.Value);
            _cache[key] = entry;
        }

        /// <inheritdoc/>
        public Task SetAsync<T>(string key, T? data, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Set(key, data, cacheTime);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SetAsync<T>(string key, T? data, TimeSpan? expiry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Set(key, data, expiry);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Func<Task<T?>>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
                Set(item.Key, await item.Value().ConfigureAwait(false), cacheTime);
        }

        /// <inheritdoc/>
        public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, Task<T?>>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
                Set(item.Key, await item.Value.ConfigureAwait(false), cacheTime);
        }

        /// <inheritdoc/>
        public async Task SetAsync<T>(IAsyncEnumerable<KeyValuePair<string, T?>> acquireKeyPair, int? cacheTime, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await foreach (var item in acquireKeyPair.WithCancellation(cancellationToken).ConfigureAwait(false))
                Set(item.Key, item.Value, cacheTime);
        }

        #endregion

        #region Removal

        /// <inheritdoc/>
        public void Remove(string key) => _cache.TryRemove(key, out _);

        /// <inheritdoc/>
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Remove(key);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Removes every key matching the Redis-style glob <paramref name="pattern"/>.
        /// </summary>
        public void RemoveByPattern(string pattern)
        {
            if (pattern is null) throw new ArgumentNullException(nameof(pattern));
            var regex = Glob.ToRegex(pattern);
            foreach (var key in _cache.Keys)
            {
                if (regex.IsMatch(key)) _cache.TryRemove(key, out _);
            }
        }

        /// <inheritdoc cref="RemoveByPattern(string)"/>
        public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RemoveByPattern(pattern);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Clear() => _cache.Clear();

        /// <inheritdoc/>
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Clear();
            return Task.CompletedTask;
        }

        #endregion

        #region Time to live

        /// <inheritdoc/>
        public TimeSpan? GetItemTimeSpanToLive(string key)
            => TryGetLiveEntry(key, out var entry) && entry.Expiry is { } expiry ? expiry - _timeProvider.GetUtcNow() : null;

        /// <inheritdoc/>
        public long GetItemTimeToLive(string key)
            => GetItemTimeSpanToLive(key) is { } ttl ? (long)ttl.TotalSeconds : -1;

        /// <inheritdoc/>
        public Task<TimeSpan?> GetItemTimeSpanToLiveAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetItemTimeSpanToLive(key));
        }

        /// <inheritdoc/>
        public Task<long> GetItemTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetItemTimeToLive(key));
        }

        #endregion

        /// <inheritdoc/>
        public void Dispose()
        {
            _cache.Clear();
            GC.SuppressFinalize(this);
        }

        private bool TryGetLiveEntry(string key, out Entry entry)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (_cache.TryGetValue(key, out entry!))
            {
                if (!IsExpired(entry)) return true;

                // Remove only this expired entry, not one written concurrently under the same key.
                ((ICollection<KeyValuePair<string, Entry>>)_cache).Remove(new KeyValuePair<string, Entry>(key, entry));
            }
            entry = null!;
            return false;
        }

        private bool HasRoomFor(string key)
        {
            if (_cache.Count < _config.MaxEntriesCount || _cache.ContainsKey(key)) return true;

            foreach (var item in _cache)
            {
                if (IsExpired(item.Value))
                    ((ICollection<KeyValuePair<string, Entry>>)_cache).Remove(item);
            }
            return _cache.Count < _config.MaxEntriesCount;
        }

        private bool IsExpired(Entry entry) => entry.Expiry is { } expiry && _timeProvider.GetUtcNow() >= expiry;

        private sealed class Entry
        {
            public Entry(string data, DateTimeOffset? expiry)
            {
                Data = data;
                Expiry = expiry;
            }

            public string Data { get; }

            public DateTimeOffset? Expiry { get; }
        }
    }
}
