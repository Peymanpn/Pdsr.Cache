# Pdsr Cache Helper


[![NuGet version (Pdsr.Cache)](https://img.shields.io/nuget/v/Pdsr.Cache.svg?style=flat-square)](https://www.nuget.org/packages/Pdsr.Cache/)

A helper library (wrapper) for caching with Redis, MSSQL, ...

## What is it?

I have started building several libraries to keep up with the DRY principle, one of them was this lib, helping me to use cache systems.

The first goal this library is keeping Cache requests in one go so I don't have to check if the cache exists, if the cache time has passed, etc.

## Getting Started

you need to install the package, add to DI and then use it in services.

1. install the package `dotnet add package Pdsr.Cache.Redis` for Redis.
2. Add the `RedisCacheManager` to DI through the extension `AddRedisCacheManager(c => c.Endpoints = "my-redis:6379")`
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

## Contribute

Please refer to [contribute](CONTRIBUTING.md).
Some parts definitely needs help.

## Documents

Under Creation.
