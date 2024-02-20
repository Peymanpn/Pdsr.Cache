using Pdsr.Cache.Configurations;

namespace Pdsr.Cache
{
    public class RedisCacheManagerExtended : RedisCacheManager, ICacheManagerExtended
    {
        private readonly IRedisConnectionFactory _redisConnectionFactory;

        public RedisCacheManagerExtended(
            IRedisConnectionFactory redisConnectionFactory,
            RedisConfiguration redisConfigurationOptions
            ) : base(redisConfigurationOptions, redisConnectionFactory)
        {
            _redisConnectionFactory = redisConnectionFactory;

        }

        public IEnumerable<string> GetIEnum(string key, Func<IEnumerable<string>> acquire, int? cacheTime = null)
        {
            if (Redis.KeyExists(key))
            {
                var smemebers = Redis.SetMembers(key);
                if (smemebers.Any())
                {
                    return smemebers.Select(c => Redis.StringGet(c.ToString()).ToString());
                }
            }
            var values = acquire();
            foreach (var item in values)
            {
                Redis.SetAdd(key, item);
            }
            throw new NotImplementedException();
        }

        public IEnumerable<int> GetIEnum(string key, Func<IEnumerable<int>> acquire, int? cacheTime = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<long> GetIEnum(string key, Func<IEnumerable<long>> acquire, int? cacheTime = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<double> GetIEnum(string key, Func<IEnumerable<double>> acquire, int? cacheTime = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetIEnumAsync(string key, Func<Task<IEnumerable<string>>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<int>> GetIEnumAsync(string key, Func<Task<IEnumerable<int>>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<long>> GetIEnumAsync(string key, Func<Task<IEnumerable<long>>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<double>> GetIEnumAsync(string key, Func<Task<IEnumerable<double>>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }


        public void SetIEnum(string key, IEnumerable<string> data, int? cacheTime = null)
        {
            foreach (string item in data)
            {
                Redis.SetAdd(key, item);
            }
        }

        public void SetIEnum(string key, IEnumerable<int> data, int? cacheTime = null)
        {
            foreach (int item in data)
            {
                // Redis.SetMembers                    
                Redis.SetAdd(key, item);
            }
        }

        public void SetIEnum(string key, IEnumerable<long> data, int? cacheTime = null)
        {
            foreach (long item in data)
            {
                Redis.SetAdd(key, item);
            }
        }

        public void SetIEnum(string key, IEnumerable<double> data, int? cacheTime = null)
        {
            foreach (double item in data)
            {
                Redis.SetAdd(key, item);
            }
        }

        public async Task SetIEnumAsync(string key, IEnumerable<string> data, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            foreach (string item in data)
            {
                await Redis.SetAddAsync(key, item);
            }
        }

        public async Task SetIEnumAsync(string key, IEnumerable<int> data, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            foreach (int item in data)
            {
                await Redis.SetAddAsync(key, item);
            }
        }

        public async Task SetIEnumAsync(string key, IEnumerable<long> data, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            foreach (long item in data)
            {
                await Redis.SetAddAsync(key, item);
            }
        }

        public async Task SetIEnumAsync(string key, IEnumerable<double> data, int? cacheTime = null, CancellationToken cancellationToken = default)
        {
            foreach (double item in data)
            {
                await Redis.SetAddAsync(key, item);
            }
        }
    }
}
