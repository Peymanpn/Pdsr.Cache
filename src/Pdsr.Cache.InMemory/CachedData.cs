namespace Pdsr.Cache.InMemory;

internal class CachedData : Dictionary<string, CacheDataItem>
{

}



internal record CacheDataItem
{
    public CacheDataItem() { }

    public CacheDataItem(DateTimeOffset e, object d)
    {
        expiry = e; data = d;
    }
    public DateTimeOffset expiry { get; set; }
    public object? data { get; set; }
}
