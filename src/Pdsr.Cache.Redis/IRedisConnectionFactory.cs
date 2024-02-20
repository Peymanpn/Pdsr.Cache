namespace Pdsr.Cache
{
    public interface IRedisConnectionFactory
    {
        ConnectionMultiplexer Connection();
    }
}
