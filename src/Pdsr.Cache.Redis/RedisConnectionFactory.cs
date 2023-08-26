using Pdsr.Cache.Configurations;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Net;
using System.Reflection;

namespace Pdsr.Cache
{
    public class RedisConnectionFactory : IRedisConnectionFactory
    {
        /// <summary>
        /// The _connection.
        /// </summary>
        private readonly Lazy<ConnectionMultiplexer> _connection;

        public RedisConnectionFactory(IRedisConfiguration redisConfigurationOptions)
        {
            if (redisConfigurationOptions is null) throw new ArgumentNullException(nameof(redisConfigurationOptions));

            IRedisConfiguration redisConfiguration = redisConfigurationOptions;

               IPEndPoint? endpoint = null;

            var endpoints = redisConfiguration?.EndPoints.Select(c
                  =>
                    {
                        var addresses = Dns.GetHostAddresses(c.Split(':')[0]);
                        var port = int.Parse(c.Split(':')[1]);
                        var ep = new IPEndPoint(addresses.First(), port);
                        return ep;
                    });
            var rediConnectConfigs = new ConfigurationOptions
            {
                Ssl = redisConfiguration is not null && redisConfiguration.UseSsl,
                AllowAdmin = redisConfiguration is not null && redisConfiguration.AllowAdmin,

                AbortOnConnectFail = redisConfiguration is not null && redisConfiguration.AbortOnConnectFail,
            };

            if (redisConfiguration is not null)
            {
                rediConnectConfigs.Password = redisConfiguration.Password;
                rediConnectConfigs.User = redisConfiguration.User;
                rediConnectConfigs.ClientName = redisConfiguration.ClientName;

                if (redisConfiguration.ClientName is not null)
                {
                    rediConnectConfigs.ClientName = redisConfiguration.ClientName;
                }
                else if (redisConfiguration.UseCallingAssemblyNameAsClientName)
                {
                    rediConnectConfigs.ClientName = Assembly.GetCallingAssembly().GetName().Name;
                }

            }
            if (endpoints == null)
            {
                if (endpoint is null)
                {
                    throw new NullReferenceException($"Only one of 'endpoint' or 'endpoints' must be provided'");
                }
                rediConnectConfigs.EndPoints.Add(endpoint);
            }
            else
                foreach (var ep in endpoints)
                {
                    rediConnectConfigs.EndPoints.Add(ep);
                }


            _connection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(rediConnectConfigs));
        }



        public ConnectionMultiplexer Connection()
        {
            return _connection.Value;
        }
    }
}
