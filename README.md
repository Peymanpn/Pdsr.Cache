# Pdsr Cache Helper


[![NuGet version (Pdsr.Cache)](https://img.shields.io/nuget/v/Pdsr.Cache.svg?style=flat-square)](https://www.nuget.org/packages/Pdsr.Cache/)

A helper library (wrapper) for caching with Redis, MSSQL, ...

## What is it?

I have started building several libraries to keep up with the DRY principle, one of them was this lib, helping me to use cache systems.

The first goal this library is keeping Cache requests in one go so I don't have to check if the cache exists, if the cache time has passed, etc.

## Getting Started

you need to install the package, add to DI and then use it in services.

1. install the package `dotnet add package Pdsr.Cache.Redis` for Redis.
2. Add the `RedisCacheManager` to DI through the extension `AddRedisCacheManager(c => { c.EndPoints = ["my-redis:6379"]; c.KeyPrefix = "my-app:"; })`
3. Instantiate the `ICacheManager` in your controller or service.
4. Use any of the Get,Set, ... methods. Async or Sync.


```csharp
public class MyClass
{
    private readonly ICacheManager _cache;
    public MyClass(ICacheManager cache) => _cache = cache;

    public async Task SomeMethod(CancellationToken cancellationToken = default)
    {
        var results = await _cache.GetAsync<SomeModel>(
            "some-key" , // cache key identifier
            async () => // Func<Task<T>> to produce something
            {
                var model = new SomeModel(); // produce something time consuming
                return model;
            },
            60, // cache time
            cancellationToken);

        return results;
    }
}

```

### Reading a value that may be missing

Use `TryGet`/`TryGetAsync` instead of `IsSet` followed by `Get`. The two-step version can race with expiry,
and `Get` returns `default` on a miss, so a cached `false` and "not cached" look the same.

```csharp
var cached = await _cache.TryGetAsync<bool>($"access:{userId}", cancellationToken);
if (cached.HasValue)
    return cached.Value;
```

### When Redis is down

Reads, writes and `IsSet` throw (`RedisConnectionException` or `RedisTimeoutException`) instead of reporting a miss.
The read-through overloads that take an `acquire` delegate fall back to it and return a correct, uncached value.

### Cancellation

Every async method takes a `CancellationToken`. Cancelling it stops the caller waiting and throws `OperationCanceledException`,
but it can't recall a command that was already sent to Redis or SQL Server. That command may still run,
so a cancelled `SetAsync` or `RemoveAsync` can still write or delete the key. A token that is already cancelled is rejected
before anything is sent.

### Sharing a Redis server

Set `KeyPrefix` so each application has its own keyspace. With a prefix, `Clear` and `RemoveByPattern`
only touch that application's keys. Without one, `Clear` refuses to run FLUSHDB unless `AllowFlushDatabase` is set.

## Upgrading to 4.0

4.0 is a breaking release:

- `ISyncCacheManager.TryGet` and `IAsyncCacheManager.TryGetAsync` were added; custom implementations must add them.
- Redis outages now throw instead of returning `default`/`false` (see above).
- `IRedisConnectionFactory` returns `IConnectionMultiplexer`, adds `ConnectionAsync`, and owns the connection;
  disposing a cache manager no longer closes it.
- `RedisConfiguration`: obsolete `Host`/`Port` removed; `UseCallingAssemblyNameAsClientName` renamed to `UseEntryAssemblyNameAsClientName`;
  `RetryCount`, `Database`, `KeyPrefix` and `AllowFlushDatabase` added. Host names are no longer resolved to fixed IPs at startup.
- Redis `Clear` no longer flushes the database by default.
- `Set<T>(key, data, TimeSpan? expiry)` no longer has a default for `expiry`; the Redis `Get`/`GetAsync` `preferReplica` parameter is now required.
- Redis sets: `RemoveFromSetItemAsync` is now `RemoveFromSetAsync` and also deletes the item; `GetSetLength` is now `GetSetLengthAsync`;
  `GetSetItemsAsync` leaves out expired items.
- A zero or negative cache time now means "don't cache" in every provider.
- `InMemoryCacheConfig.MaxEnteriesCount` is now `MaxEntriesCount`; in-memory patterns use Redis glob syntax instead of regex.
- `SqlCacheManager.RemoveByPattern`/`Clear` throw `NotSupportedException`.
- DI extension classes were renamed (`RedisServiceCollectionExtensions`, …); the extension methods themselves are unchanged except that
  the Redis service lifetime parameters were removed (everything is a singleton).
- Removed: `Pdsr.Cache.Polly`, `ICacheManagerExtended` and `RedisCacheManagerExtended`, and the non-generic `Set`/`SetAsync`/`Get` overloads.

## Contribute

Please refer to [contribute](CONTRIBUTING.md).
Some parts definitely needs help.

## Documents

Under Creation.
