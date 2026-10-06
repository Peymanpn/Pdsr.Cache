namespace Pdsr.Cache.InMemory.Configurations
{
    /// <summary>
    /// Settings for <see cref="InMemoryCacheManager"/>.
    /// </summary>
    public class InMemoryCacheConfig
    {
        /// <summary>
        /// Maximum number of entries. When the cache is full, expired entries are evicted;
        /// if it is still full, new keys are not cached (existing keys can still be updated).
        /// </summary>
        public int MaxEntriesCount { get; set; } = 10240;
    }
}
