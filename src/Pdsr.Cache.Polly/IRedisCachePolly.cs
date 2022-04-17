using Pdsr.Cache.Configurations;
using Pdsr.Cache.Polly.Configurations;
using Microsoft.Extensions.Options;
using Polly;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pdsr.Cache.Polly
{
    public interface IRedisCachePolly : IRedisCacheManager
    {

    }

    /// <summary>
    /// CacheManager functions with resiliency.
    /// the aim is prevent <see cref="RedisConnectionException"/> exception
    /// </summary>
    public class RedisCachePolly : RedisCacheManager, IRedisCachePolly
    {
        private readonly ICachePollyConfigurations _cachePollyConfigurations;
        private readonly RedisCacheManagerPollyRegistry _pollyRegistry;

        public RedisCachePolly(
            ICachePollyConfigurations cachePollyConfigurations,
            // IRedisConfiguration redisConfiguration,
            IRedisConfiguration redisConfigurationOptions,
            IRedisConnectionFactory redisConnectionFactory,
            RedisCacheManagerPollyRegistry redisCacheManagerPollyRegistry
            ) : base(redisConfigurationOptions, redisConnectionFactory)
        {
            _cachePollyConfigurations = cachePollyConfigurations;
            _pollyRegistry = redisCacheManagerPollyRegistry;
            _pollyRegistry.DefaultRetryPolicy ??=
                Policy.Handle<RedisConnectionException>()
                    .WaitAndRetryAsync(_cachePollyConfigurations.RetryCount,
                        r => TimeSpan.FromMilliseconds(Math.Pow(2, r)));
        }

        private IAsyncPolicy? GetRetryPolicy()
            => _pollyRegistry.GetRetryPolicy ?? _pollyRegistry.DefaultRetryPolicy;

        private IAsyncPolicy? SetRetryPolicy()
            => _pollyRegistry.SetRetryPolicy ?? _pollyRegistry.DefaultRetryPolicy;


        //public override Task<T?> GetAsync<T>(string key, Task<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        //{
        //    var retryPolicy = GetRetryPolicy();
        //    if (retryPolicy is null)
        //    {
        //        return base.GetAsync(key, acquire, cacheTime, cancellationToken);
        //    }
        //    return GetRetryPolicy().ExecuteAsync(() => base.GetAsync(key, acquire, cacheTime, cancellationToken));
        //}

        ////<inheritdoc>
        //public override Task<T?> GetAsync<T>(string key, Func<T?> acquire, int? cacheTime = null, CancellationToken cancellationToken = default)
        //    => GetRetryPolicy().ExecuteAsync(() => base.GetAsync(key, acquire, cacheTime, cancellationToken));

        //public override Task SetAsync(string key, object data, int? cacheTime = null, CancellationToken cancellation = default)
        //    => SetRetryPolicy().ExecuteAsync(() => base.SetAsync(key, data, cacheTime, cancellation));


    }
}
