using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pdsr.Cache.InMemory;

internal class CachedData : ConcurrentDictionary<string, CacheDataItem>
{

}



internal record CacheDataItem
{
    public CacheDataItem() { }

    public CacheDataItem(DateTimeOffset e, string? d)
    {
        Expiry = e;
        Data = d;
    }
    public DateTimeOffset Expiry { get; set; }
    public string? Data { get; set; }
}
