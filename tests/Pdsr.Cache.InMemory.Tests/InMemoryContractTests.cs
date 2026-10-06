using Pdsr.Cache.InMemory.Configurations;
using Pdsr.Cache.Tests.Shared;

namespace Pdsr.Cache.InMemory.Tests;

public class InMemoryContractTests : CacheManagerContractTests
{
    protected override ICacheManager Cache { get; } = new InMemoryCacheManager(new InMemoryCacheConfig());
}
