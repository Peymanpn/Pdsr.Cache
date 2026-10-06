using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Pdsr.Cache.Tests.Shared;

namespace Pdsr.Cache.SqlServer.Tests;

/// <summary>
/// The shared contract over an in-memory <see cref="IDistributedCache"/>, which behaves like the SQL Server one.
/// </summary>
public class SqlContractTests : CacheManagerContractTests
{
    protected override ICacheManager Cache { get; } =
        new SqlCacheManager(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    protected override bool SupportsTimeToLive => false;

    protected override bool SupportsKeyEnumeration => false;
}
