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

            if (redisConfiguration.EndPoints != null && !string.IsNullOrEmpty(redisConfiguration.Host))
                throw new InvalidOperationException("Only one of 'Endpoints' or 'Host/Port' must be provided.");

            else if (redisConfiguration.EndPoints == null && string.IsNullOrEmpty(redisConfiguration.Host))
                throw new InvalidOperationException("'Endpoints' or 'Host/Port' must be provided.");

            else if (redisConfiguration.EndPoints is not null && !redisConfiguration.EndPoints.Any(e => e.Contains(':')))
                throw new InvalidOperationException("'Endpoints must be a valid IP Address/Host name with Port. ie: example.com:6379 or 192.168.1.1:6379");

            IPEndPoint? endpoint = null;

            if (!string.IsNullOrEmpty(redisConfiguration.Host))
            {
                if (redisConfiguration.Port is null) { throw new ArgumentException("Port must be provided along with Host"); }
                var ipAddresses = Dns.GetHostAddresses(redisConfiguration.Host);
                if (ipAddresses is not null && ipAddresses.Length > 0)
                {
                    endpoint = new IPEndPoint(ipAddresses[0], redisConfiguration.Port.Value);
                }
            }

            var endpoints = redisConfiguration?.EndPoints?.Select(c
                  =>
                    {
                        var addresses = Dns.GetHostAddresses(c.Split(':')[0]);
                        var port = int.Parse(c.Split(':')[1]);
                        var ep = new IPEndPoint(addresses.First(), port);
                        return ep;
                    });
            var rediConnectConfigs = new ConfigurationOptions
            {
                // EndPoints = { endpoint },
                // EndPoints = endpoints ,
                Ssl = redisConfiguration is not null && redisConfiguration.UseSsl,
                AllowAdmin = redisConfiguration is not null && redisConfiguration.AllowAdmin,

                AbortOnConnectFail = redisConfiguration is not null && redisConfiguration.AbortOnConnectFail,
            };
            if (redisConfiguration is not null)
            {
                if (redisConfiguration.Password is not null)
                {
                    rediConnectConfigs.Password = redisConfiguration.Password;
                }
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
                    throw new NullReferenceException($"One of 'endpoint' or 'endpoints' must be provided'");
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
