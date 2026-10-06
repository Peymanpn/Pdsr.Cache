using Pdsr.Cache.Tests.Shared;

namespace Pdsr.Cache.Redis.Tests;

/// <summary>
/// The shared cache contract, run against a real Redis server.
/// </summary>
public class RedisContractTests : CacheManagerContractTests
{
    private readonly RedisCacheManager _cache;

    public RedisContractTests(RedisFixture redis)
    {
        redis.RequireDocker();
        _cache = redis.CreateCache();
    }

    protected override ICacheManager Cache => _cache;
}
