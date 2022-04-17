using Pdsr.Cache;
using Pdsr.Cache.Configurations;
using Pdsr.Cache.Polly;
using Pdsr.Cache.Polly.Configurations;
using Polly;
using StackExchange.Redis;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds RedisCacheManager With default retry policy
        /// number of retries are being set in <see cref="RedisConfiguration.RetryCount"/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConfiguration"></param>
        public static void AddRedisPollyCacheManager(this IServiceCollection services, IRedisConfiguration redisConfiguration, ICachePollyConfigurations cachePollyConfigurations)
        {
            services.AddRedisCacheManager(redisConfiguration);
            services.AddSingleton<IRedisCacheManager, RedisCachePolly>();
            services.AddSingleton(cachePollyConfigurations);
            services.AddSingleton<RedisCacheManagerPollyRegistry>(f =>
            {
                return new RedisCacheManagerPollyRegistry(
                   Policy.Handle<RedisConnectionException>()
                         .WaitAndRetryAsync(cachePollyConfigurations.RetryCount,
                         r => TimeSpan.FromMilliseconds(Math.Pow(2, r))),
                   Policy.Handle<RedisConnectionException>()
                         .WaitAndRetryAsync(cachePollyConfigurations.RetryCount,
                         r => TimeSpan.FromMilliseconds(Math.Pow(2, r))),
                   Policy.Handle<RedisConnectionException>()
                         .WaitAndRetryAsync(cachePollyConfigurations.RetryCount,
                         r => TimeSpan.FromMilliseconds(Math.Pow(2, r)))
                    );
            });
        }


        public static void AddRedisPollyCacheManager(this IServiceCollection services,
            IRedisConfiguration redisConfiguration,
            IAsyncPolicy retryPolicyDefault,
            IAsyncPolicy? retryPolicyGet = null,
            IAsyncPolicy? retryPolicySet = null)
        {
            services.AddRedisCacheManager(redisConfiguration);
            services.AddSingleton<IRedisCacheManager, RedisCachePolly>();
            services.AddSingleton<RedisCacheManagerPollyRegistry>(f =>
                new RedisCacheManagerPollyRegistry(
                    retryPolicyDefault,
                    retryPolicyGet,
                    retryPolicySet)
                );
        }

        public static RedisCacheManagerPollyRegistry AddRedisPollyCacheManagerBuilder(this IServiceCollection services,
            IRedisConfiguration redisConfiguration)
        {
            services.AddRedisCacheManager(redisConfiguration);
            services.AddSingleton<IRedisCacheManager, RedisCachePolly>();
            var registry = new RedisCacheManagerPollyRegistry();
            services.AddSingleton(f => registry);
            return registry;
        }

        public static RedisCacheManagerPollyRegistry AddDefaultPolicy<TException>(
            this RedisCacheManagerPollyRegistry registry,
            Func<PolicyBuilder, IAsyncPolicy> builder)
            where TException : Exception
        {
            registry.DefaultRetryPolicy = builder(Policy.Handle<TException>());
            return registry;
        }

        public static RedisCacheManagerPollyRegistry AddGetPolicy(this RedisCacheManagerPollyRegistry registry,
            Func<IAsyncPolicy> builder)
        {
            registry.GetRetryPolicy = builder();
            return registry;
        }

        public static RedisCacheManagerPollyRegistry AddGetPolicy<TException>(this RedisCacheManagerPollyRegistry registry,
            Func<PolicyBuilder, IAsyncPolicy> builder) where TException : Exception
        {
            registry.GetRetryPolicy = builder(Policy.Handle<TException>());
            return registry;
        }

        public static RedisCacheManagerPollyRegistry AddSetPolicy(this RedisCacheManagerPollyRegistry registry,
            Func<IAsyncPolicy> builder)
        {
            registry.SetRetryPolicy = builder();
            return registry;
        }

        public static RedisCacheManagerPollyRegistry AddSetPolicy<TException>(this RedisCacheManagerPollyRegistry registry,
            Func<PolicyBuilder, IAsyncPolicy> builder) where TException : Exception
        {
            registry.SetRetryPolicy = builder(Policy.Handle<TException>());
            return registry;
        }
    }
}
