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

How long a call takes to fail depends on `FailFastWhenDisconnected`:

| Setting | While disconnected, each call | Read-through with `acquire` |
|---|---|---|
| `false` (default) | waits in the client's queue for `AsyncTimeout`/`SyncTimeout` (5 s unless set), then throws | waits the timeout on the read, then calls `acquire` |
| `true` | throws `RedisConnectionException` immediately | calls `acquire` immediately |

Turn it on when the cache is an optimisation and callers fall back to the source: otherwise every request pays the
timeout for as long as the outage lasts. Leave it off if you'd rather ride out short reconnects, since queued commands
are sent once the connection is back instead of failing. Either way you can bound the wait with `AsyncTimeout` and
`SyncTimeout` (milliseconds).

```csharp
services.AddRedisCacheManager(c =>
{
    c.EndPoints = ["my-redis:6379"];
    c.FailFastWhenDisconnected = true; // fail immediately during an outage
    c.AsyncTimeout = 1000;             // and give up on a slow command after 1 s
});
```

The first connect is separate. Calls made while the first connection attempt is still running wait for that attempt
(around 2 s against a host that refuses connections, up to the client's `ConnectTimeout` against one that doesn't answer).
After a failed attempt, with `AbortOnConnectFail = false`, the client keeps retrying in the background and calls follow the
table above.

`FailFastWhenDisconnected`, `AsyncTimeout` and `SyncTimeout` live on `IRedisTimeoutConfiguration`, which
`RedisConfiguration` implements. A custom `IRedisConfiguration` keeps the client defaults unless it implements that interface too.

### Cancellation

Every async method takes a `CancellationToken`. Cancelling it stops the caller waiting and throws `OperationCanceledException`,
but it can't recall a command that was already sent to Redis or SQL Server. That command may still run,
so a cancelled `SetAsync` or `RemoveAsync` can still write or delete the key. A token that is already cancelled is rejected
before anything is sent.

### Sharing a Redis server

Set `KeyPrefix` so each application has its own keyspace. With a prefix, `Clear` and `RemoveByPattern`
only touch that application's keys. Without one, `Clear` refuses to run FLUSHDB unless `AllowFlushDatabase` is set.

## Changes in 4.1

- New `FailFastWhenDisconnected`, `AsyncTimeout` and `SyncTimeout` settings control how long calls wait while Redis is
  unreachable (see "When Redis is down"). They're opt-in, so 4.0's behaviour is unchanged unless you set them.
- They're on a new optional interface, `IRedisTimeoutConfiguration`, so existing `IRedisConfiguration` implementations
  keep compiling.

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
