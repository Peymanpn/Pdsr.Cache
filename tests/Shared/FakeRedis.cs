using NSubstitute;
using StackExchange.Redis;

namespace Pdsr.Cache.Tests.Shared;

/// <summary>
/// Substitutes for the StackExchange.Redis surface the cache manager uses.
/// </summary>
internal sealed class FakeRedis
{
    private readonly List<IServer> _servers = [];

    public FakeRedis()
    {
        Connection.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(Database);
        Connection.GetServers().Returns(_ => _servers.ToArray());
        Factory.Connection().Returns(Connection);
        Factory.ConnectionAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Connection));
    }

    public IConnectionMultiplexer Connection { get; } = Substitute.For<IConnectionMultiplexer>();

    public IDatabase Database { get; } = Substitute.For<IDatabase>();

    public IRedisConnectionFactory Factory { get; } = Substitute.For<IRedisConnectionFactory>();

    public IServer AddServer(string endPoint, bool replica = false, bool connected = true, params string[] keys)
    {
        var server = Substitute.For<IServer>();
        server.IsReplica.Returns(replica);
        server.IsConnected.Returns(connected);
        server.EndPoint.Returns(EndPointCollection.TryParse(endPoint)!);
        server.KeysAsync(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(_ => AsyncSequence.From(keys.Select(k => (RedisKey)k).ToArray()));
        server.Keys(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(_ => keys.Select(k => (RedisKey)k));
        _servers.Add(server);
        return server;
    }

    public static RedisConnectionException ConnectionError()
        => new(ConnectionFailureType.UnableToConnect, CommandFlags.None, "Redis is down", null, CommandStatus.Unknown);

    public static RedisTimeoutException TimeoutError() => new(CommandFlags.None, "Timed out", CommandStatus.Sent);

    public static RedisValue Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);
}
