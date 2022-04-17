using StackExchange.Redis;

namespace Pdsr.Cache
{
    public interface IRedisConnectionFactory
    {
        ConnectionMultiplexer Connection();
    }
}
