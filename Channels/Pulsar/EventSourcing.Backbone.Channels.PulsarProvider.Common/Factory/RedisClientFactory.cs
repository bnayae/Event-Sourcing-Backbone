using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using static EventSourcing.Backbone.Channels.RedisProvider.Common.RedisChannelConstants;
using static EventSourcing.Backbone.Private.EventSourceTelemetry;

namespace EventSourcing.Backbone;
/// <summary>
/// REDIS client factory
/// </summary>
public static class RedisClientFactory
{
    private static int _index = 0;
    private const string CONNECTION_NAME_PATTERN = "ev-src:{0}:{1}:{2}";
    private static readonly string? ASSEMBLY_NAME = Assembly.GetEntryAssembly()?.GetName()?.Name?.ToDash();
    private static readonly Version? ASSEMBLY_VERSION = Assembly.GetEntryAssembly()?.GetName()?.Version;
    private static readonly Counter<int> ReConnectCounter = EMeter.CreateCounter<int>("evt-src.redis.re-connect.succeed", "count",
                                                                            "succeed to re-connect to redis");
    private static readonly Counter<int> ReConnectFailCounter = EMeter.CreateCounter<int>("evt-src.redis.re-connect.failure", "count",
                                                                            "failure to re-connect to redis");

    #region CreateConfigurationOptions

    /// <summary>
    /// Create REDIS configuration options.
    /// </summary>
    /// <param name="configurationHook">A configuration hook.</param>
    /// <returns></returns>
    public static ConfigurationOptions CreateConfigurationOptions(
                Action<ConfigurationOptions>? configurationHook = null)
    {
        IRedisCredentials credential = new RedisCredentialsEnvKeys();
        var redis = credential.CreateConfigurationOptions(configurationHook);
        return redis;
    }

    /// <summary>
    /// Create REDIS configuration options.
    /// </summary>
    /// <param name="endpoint">The raw endpoint (not an environment variable).</param>
    /// <param name="password">The password (not an environment variable).</param>
    /// <param name="configurationHook">A configuration hook.</param>
    /// <returns></returns>
    public static ConfigurationOptions CreateConfigurationOptions(
                string endpoint,
                string? password = null,
                Action<ConfigurationOptions>? configurationHook = null)
    {
        IRedisCredentials credential = new RedisCredentialsRaw(endpoint, password);
        var redis = credential.CreateConfigurationOptions(configurationHook);
        return redis;
    }

    /// <summary>
    /// Create REDIS configuration options.
    /// </summary>
    /// <param name="credential">The credential's environment keys.</param>
    /// <param name="configurationHook">A configuration hook.</param>
    /// <returns></returns>
    public static ConfigurationOptions CreateConfigurationOptions(
                this IRedisCredentials credential,
                Action<ConfigurationOptions>? configurationHook = null)
    {
        var (endpoint, password) = credential switch
        {
            RedisCredentialsRaw raw => (raw.Endpoint, raw.Password),
            RedisCredentialsEnvKeys env => (
                                                Environment.GetEnvironmentVariable(env.Endpoint ?? END_POINT_KEY),
                                                Environment.GetEnvironmentVariable(env.Password ?? PASSWORD_KEY)
                                            ),
            _ => throw new InvalidOperationException(credential?.GetType()?.Name)
        };

        #region Validation

        if (string.IsNullOrEmpty(endpoint))
            throw new InvalidOperationException($"{nameof(endpoint)} is null");

        #endregion // Validation

        // https://stackexchange.github.io/StackExchange.Redis/Configuration.html
        var configuration = ConfigurationOptions.Parse(endpoint);
        configuration.Password = password;
        if (configurationHook != null)
            configuration.Apply(configurationHook);

        return configuration;
    }

    #endregion // CreateConfigurationOptions

    #region CreateProviderAsync

    /// <summary>
    /// Create REDIS client.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configurationHook">A configuration hook.</param>
    /// <returns></returns>
    public static async Task<IConnectionMultiplexer> CreateProviderAsync(
                ILogger? logger = null,
                Action<ConfigurationOptions>? configurationHook = null)
    {
        IRedisCredentials credential = new RedisCredentialsEnvKeys();
        var redis = await credential.CreateProviderAsync(logger, configurationHook);
        return redis.WithTelemetry();
    }

    /// <summary>
    /// Create REDIS client.
    /// </summary>
    /// <param name="endpoint">The raw endpoint (not an environment variable).</param>
    /// <param name="password">The password (not an environment variable).</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configurationHook">A configuration hook.</param>
    /// <returns></returns>
    public static async Task<IConnectionMultiplexer> CreateProviderAsync(
                string endpoint,
                string? password = null,
                ILogger? logger = null,
                Action<ConfigurationOptions>? configurationHook = null)
    {
        IRedisCredentials credential = new RedisCredentialsRaw(endpoint, password);
        var redis = await credential.CreateProviderAsync(logger, configurationHook);
        return redis.WithTelemetry();
    }

    /// <summary>
    /// Create REDIS client.
    /// </summary>
    /// <param name="credential">The credential's environment keys.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configurationHook">A configuration hook.</param>
    /// <returns></returns>
    public static async Task<IConnectionMultiplexer> CreateProviderAsync(
                this IRedisCredentials credential,
                ILogger? logger = null,
                Action<ConfigurationOptions>? configurationHook = null)
    {
        try
        {
            // https://stackexchange.github.io/StackExchange.Redis/Configuration.html
            var configuration = credential.CreateConfigurationOptions(configurationHook);
            var redis = await configuration.CreateProviderAsync(logger);
            return redis.WithTelemetry();
        }
        catch (Exception ex)
        {
            if (logger != null)
                logger.LogError(ex.FormatLazy(), "REDIS CONNECTION Setting ERROR");
            else
                Console.WriteLine($"REDIS CONNECTION Setting ERROR: {ex.FormatLazy()}");
            throw;
        }
    }

    /// <summary>
    /// Create REDIS client.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns></returns>
    /// <exception cref="StringBuilder">
    /// </exception>
    /// <exception cref="StackExchange.Redis.RedisConnectionException">Fail to establish REDIS connection</exception>
    /// <exception cref="RedisConnectionException">Fail to establish REDIS connection</exception>
    public static async Task<IConnectionMultiplexer> CreateProviderAsync(
                this ConfigurationOptions configuration,
                ILogger? logger = null)
    {
        try
        {
            var sb = new StringBuilder();
            var writer = new StringWriter(sb);

            // https://stackexchange.github.io/StackExchange.Redis/Configuration.html
            configuration.ClientName = string.Format(
                                    CONNECTION_NAME_PATTERN,
                                    ASSEMBLY_NAME,
                                    ASSEMBLY_VERSION,
                                    Interlocked.Increment(ref _index));

            configuration = configuration.Apply(cfg =>
            {
                // keep retry to get connection on failure
                cfg.AbortOnConnectFail = false;
#pragma warning disable S125 
                /* 
                   cfg.ConnectTimeout = 15;
                   cfg.SyncTimeout = 10;
                   cfg.AsyncTimeout = 10;
                   cfg.DefaultDatabase = Debugger.IsAttached ? 1 : null;
                */
#pragma warning restore S125 
            }
            );


            string endpoints = string.Join(";", configuration.EndPoints);
            using var trace = "redis-connect".StartTrace(t => t.Add("endpoints", endpoints), ActivityKind.Client);
            IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(configuration, writer);
            if (logger != null)
                logger.LogInformation("REDIS Connection [{envKey}]: {info} succeed",
                    endpoints,
                    sb);
            else
                Console.WriteLine($"REDIS Connection [{endpoints}] succeed: {sb}");
            redis.ConnectionFailed += OnConnectionFailed;
            redis.ErrorMessage += OnConnErrorMessage;
            redis.InternalError += OnInternalConnError;
            ReConnectCounter.Add(1);
            return redis.WithTelemetry();
        }
        catch (Exception ex)
        {
            ReConnectFailCounter.Add(1);
            if (logger != null)
                logger.LogError(ex.FormatLazy(), "REDIS CONNECTION ERROR");
            else
                Console.WriteLine($"REDIS CONNECTION ERROR: {ex.FormatLazy()}");
            throw;
        }

        #region Event Handlers

        void OnInternalConnError(object? sender, InternalErrorEventArgs e)
        {
            if (logger != null)
            {
                logger.LogError(e.Exception, "REDIS Connection internal failure: Failure type = {typeOfConnection}, Origin = {typeOfFailure}",
                                             e.ConnectionType, e.Origin);
            }
            else
                Console.WriteLine($"REDIS Connection internal failure: Failure type = {e.ConnectionType}, Origin = {e.Origin}");
        }

        void OnConnErrorMessage(object? sender, RedisErrorEventArgs e)
        {
            if (logger != null)
            {
                logger.LogWarning("REDIS Connection error: {message}",
                                    e.Message);
            }
            else
                Console.WriteLine($"REDIS Connection error: {e.Message}");
        }


        void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
            if (logger != null)
            {
                logger.LogError(e.Exception, "REDIS Connection failure: Failure type = {typeOfConnection}, Failure type = {typeOfFailure}", e.ConnectionType, e.FailureType);
            }
            else
                Console.WriteLine($"REDIS Connection failure: Failure type = {e.ConnectionType}, Failure type = {e.FailureType}");
        }

        #endregion // Event Handlers

    }

    #endregion // CreateProviderAsync
}
