using Pdsr.Cache.Configurations;
using Pdsr.Cache.Internal;
using System.Reflection;

namespace Pdsr.Cache;

/// <summary>
/// Creates and owns a single <see cref="IConnectionMultiplexer"/> for an <see cref="IRedisConfiguration"/>.
/// A failed connect is not cached: the next call tries again.
/// </summary>
public class RedisConnectionFactory : IRedisConnectionFactory
{
    private readonly object _gate = new();
    private Task<IConnectionMultiplexer>? _connection;
    private bool _disposed;

    /// <summary>
    /// Creates the factory. Configuration is validated here; no connection is made until first use.
    /// </summary>
    /// <exception cref="ArgumentException">No endpoints are configured, or one can't be parsed.</exception>
    public RedisConnectionFactory(IRedisConfiguration redisConfiguration)
    {
        Options = CreateConfigurationOptions(redisConfiguration);
    }

    /// <summary>
    /// The options the connection is created with.
    /// </summary>
    public ConfigurationOptions Options { get; }

    /// <summary>
    /// Translates <paramref name="configuration"/> into StackExchange.Redis options.
    /// </summary>
    /// <exception cref="ArgumentException">No endpoints are configured, or one can't be parsed.</exception>
    public static ConfigurationOptions CreateConfigurationOptions(IRedisConfiguration configuration)
    {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));

        var options = new ConfigurationOptions
        {
            Ssl = configuration.UseSsl,
            AllowAdmin = configuration.AllowAdmin,
            AbortOnConnectFail = configuration.AbortOnConnectFail,
            User = configuration.User,
            Password = configuration.Password,
            ConnectRetry = Math.Max(0, configuration.RetryCount),
            DefaultDatabase = configuration.Database >= 0 ? configuration.Database : null,
            ClientName = configuration.ClientName
                ?? (configuration.UseEntryAssemblyNameAsClientName ? Assembly.GetEntryAssembly()?.GetName().Name : null),
        };

        var endpoints = configuration.EndPoints ?? [];
        if (endpoints.Length == 0)
            throw new ArgumentException("At least one Redis endpoint is required.", nameof(configuration));

        foreach (var endpoint in endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Redis endpoints can't be empty.", nameof(configuration));

            // Added as text, so host names stay DnsEndPoints rather than being pinned to an IP.
            // Handles "host", "host:port", "1.2.3.4:port" and "[::1]:port".
            options.EndPoints.Add(endpoint.Trim());
        }

        return options;
    }

    /// <inheritdoc/>
    public IConnectionMultiplexer Connection()
    {
        Task<IConnectionMultiplexer> pending;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_connection is null || _connection.IsFaulted || _connection.IsCanceled)
            {
                var connection = Connect(Options);
                _connection = Task.FromResult(connection);
                return connection;
            }
            pending = _connection;
        }
        return pending.GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public Task<IConnectionMultiplexer> ConnectionAsync(CancellationToken cancellationToken = default)
    {
        Task<IConnectionMultiplexer> pending;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_connection is null || _connection.IsFaulted || _connection.IsCanceled)
            {
                _connection = StartConnect();
            }
            pending = _connection;
        }
        return pending.WithCancellation(cancellationToken);
    }

    /// <summary>
    /// Opens a connection synchronously. Override to substitute the transport.
    /// </summary>
    protected virtual IConnectionMultiplexer Connect(ConfigurationOptions options)
        => ConnectionMultiplexer.Connect(options);

    /// <summary>
    /// Opens a connection asynchronously. Override to substitute the transport.
    /// </summary>
    protected virtual async Task<IConnectionMultiplexer> ConnectAsync(ConfigurationOptions options)
        => await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);

    private Task<IConnectionMultiplexer> StartConnect()
    {
        try
        {
            return ConnectAsync(Options);
        }
        catch (Exception ex)
        {
            var failed = new TaskCompletionSource<IConnectionMultiplexer>();
            failed.SetException(ex);
            return failed.Task;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RedisConnectionFactory));
    }

    /// <summary>
    /// Closes the connection, if one was opened.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Closes the connection, if one was opened.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        Task<IConnectionMultiplexer>? connection;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            connection = _connection;
            _connection = null;
        }

        if (!disposing || connection is null) return;

        // A connect still in flight is disposed once it completes.
        connection.ContinueWith(
            t => t.Result.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
