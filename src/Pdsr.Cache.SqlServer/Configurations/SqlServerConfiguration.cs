using System;

namespace Pdsr.Cache.SqlServer.Configurations
{
    public class SqlServerConfiguration
    {
        public string? ConnectionString { get; set; }
        public string? TableName { get; set; }
        public string? SchemaName { get; set; }
        public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(20);
        public TimeSpan? ExpiredItemsDeletionInterval { get; set; } = TimeSpan.FromMinutes(30);
    }
}
