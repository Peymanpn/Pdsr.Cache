using Pdsr.Cache.Internal;
using System.Text;

namespace Pdsr.Cache;

public partial class RedisCacheManager
{
    private const int ScanPageSize = 250;

    /// <inheritdoc/>
    public IServer Server
    {
        get
        {
            var servers = _connectionFactory.Connection().GetServers();
            return servers.FirstOrDefault(s => !s.IsReplica && s.IsConnected) ?? servers[0];
        }
    }

    /// <inheritdoc/>
    public IEnumerable<string> Keys => ScanKeys("*");

    /// <inheritdoc/>
    public IEnumerable<string> GetKeysForUser(string subjectId) => GetKeysForUserByPrefix(subjectId);

    /// <inheritdoc/>
    public IEnumerable<string> GetKeysForUserByPrefix(string subjectId, string prefix = "????:")
        => ScanKeys($"{prefix}{subjectId}:*");

    /// <summary>
    /// Removes every key matching the glob <paramref name="pattern"/>, on every primary server.
    /// With a <see cref="Configurations.IRedisConfiguration.KeyPrefix"/>, only keys under the prefix are matched.
    /// </summary>
    /// <exception cref="RedisConnectionException">A primary server is not connected, so its keys can't be removed.</exception>
    public void RemoveByPattern(string pattern) => RemoveByPatternAsync(pattern).GetAwaiter().GetResult();

    /// <inheritdoc cref="RemoveByPattern(string)"/>
    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (pattern is null) throw new ArgumentNullException(nameof(pattern));
        return DeleteMatchingAsync(EscapeGlob(_keyPrefix) + pattern, cancellationToken);
    }

    /// <summary>
    /// With a <see cref="Configurations.IRedisConfiguration.KeyPrefix"/>, removes only this cache's keys.
    /// Without one, runs FLUSHDB on every primary, which is refused unless
    /// <see cref="Configurations.IRedisConfiguration.AllowFlushDatabase"/> is set.
    /// </summary>
    /// <exception cref="InvalidOperationException">No key prefix is set and flushing the database isn't allowed.</exception>
    public void Clear() => ClearAsync().GetAwaiter().GetResult();

    /// <inheritdoc cref="Clear"/>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_keyPrefix.Length > 0)
        {
            await DeleteMatchingAsync(EscapeGlob(_keyPrefix) + "*", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!_configuration.AllowFlushDatabase)
            throw new InvalidOperationException(
                "Clear would run FLUSHDB and delete every key in the database, including keys other applications wrote. " +
                "Set KeyPrefix to clear only this cache's keys, or set AllowFlushDatabase (with AllowAdmin) to allow flushing.");

        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _connectionFactory.ConnectionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var server in PrimaryServers(connection))
            await server.FlushDatabaseAsync(_configuration.Database).WithCancellation(cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteMatchingAsync(string rawPattern, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _connectionFactory.ConnectionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var server in PrimaryServers(connection))
        {
            // Each key is deleted on the server it was found on: routing through IDatabase would send every DEL
            // to a single primary. Single-key and pipelined, because a multi-key DEL fails across cluster slots.
            var pending = new List<Task>(ScanPageSize);
            await foreach (var key in server.KeysAsync(_configuration.Database, rawPattern, ScanPageSize).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                pending.Add(DeleteOnServerAsync(server, key));
                if (pending.Count == ScanPageSize)
                {
                    await Task.WhenAll(pending).WithCancellation(cancellationToken).ConfigureAwait(false);
                    pending.Clear();
                }
            }
            await Task.WhenAll(pending).WithCancellation(cancellationToken).ConfigureAwait(false);
        }
    }

    private Task DeleteOnServerAsync(IServer server, RedisKey key)
        => server.ExecuteAsync(_configuration.Database >= 0 ? _configuration.Database : null, "DEL", [key]);

    private List<string> ScanKeys(string pattern)
    {
        var rawPattern = EscapeGlob(_keyPrefix) + pattern;
        var connection = _connectionFactory.Connection();
        return PrimaryServers(connection)
            .SelectMany(server => server.Keys(_configuration.Database, rawPattern, ScanPageSize))
            .Select(key => ((string)key!).Substring(_keyPrefix.Length))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Every primary, so scans see the whole keyspace. Fails rather than silently skipping a server that is down.
    /// </summary>
    private static IServer[] PrimaryServers(IConnectionMultiplexer connection)
    {
        var primaries = connection.GetServers().Where(s => !s.IsReplica).ToArray();
        if (primaries.Length == 0)
            throw Unavailable("No Redis primary server is available.");

        var disconnected = primaries.FirstOrDefault(s => !s.IsConnected);
        if (disconnected is not null)
            throw Unavailable(
                $"Redis server {disconnected.EndPoint} is not connected, so its keys can't be scanned.");

        return primaries;
    }

    private static RedisConnectionException Unavailable(string message)
        => new(ConnectionFailureType.UnableToConnect, CommandFlags.None, message, null, CommandStatus.Unknown);

    /// <summary>
    /// Escapes glob metacharacters so a key prefix matches literally.
    /// </summary>
    internal static string EscapeGlob(string text)
    {
        if (text.IndexOfAny(['*', '?', '[', ']', '\\']) < 0) return text;

        var escaped = new StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            if (c is '*' or '?' or '[' or ']' or '\\') escaped.Append('\\');
            escaped.Append(c);
        }
        return escaped.ToString();
    }
}
