using EventSourcing.Backbone.Building;
using EventSourcing.Backbone.Channels;
using EventSourcing.Backbone.Channels.RedisProvider;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace EventSourcing.Backbone;

public static class RedisConsumerBuilder
{
    #region Create

    /// <summary>
    /// Create REDIS consumer builder.
    /// </summary>
    /// <param name="endpoint">The raw endpoint (not an environment variable).</param>
    /// <param name="password">The password (not an environment variable).</param>
    /// <param name="configurationHook">The configuration hook.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder Create(
                        string endpoint,
                        string? password = null,
                        Action<ConfigurationOptions>? configurationHook = null)
    {
        var configuration = RedisClientFactory.CreateConfigurationOptions(endpoint, password, configurationHook);
        return configuration.CreateRedisConsumerBuilder();
    }

    #endregion // Create

    #region CreateRedisConsumerBuilder

    /// <summary>
    /// Create REDIS consumer builder.
    /// </summary>
    /// <param name="configurationHook">The configuration hook.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder Create(
                        Action<ConfigurationOptions>? configurationHook = null)
    {
        var configuration = RedisClientFactory.CreateConfigurationOptions(configurationHook);
        return configuration.CreateRedisConsumerBuilder();
    }

    /// <summary>
    /// Create REDIS consumer builder.
    /// </summary>
    /// <param name="options">The redis configuration.</param>
    /// <param name="setting">The setting.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder CreateRedisConsumerBuilder(
                        this ConfigurationOptions options,
                        RedisConsumerChannelSetting? setting = null)
    {
        var stg = setting ?? RedisConsumerChannelSetting.Default;
        var builder = ConsumerBuilder.Empty;
        var channelBuilder = builder.UseChannel(LocalCreate);
        return channelBuilder;

        IConsumerChannelProvider LocalCreate(ILogger logger)
        {
            var channel = new RedisConsumerChannel(
                                    logger,
                                    options,
                                    stg);
            return channel;
        }
    }

    /// <summary>
    /// Create REDIS consumer builder.
    /// </summary>
    /// <param name="setting">The setting.</param>
    /// <param name="configurationHook">The configuration hook.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder CreateRedisConsumerBuilder(
                        this RedisConsumerChannelSetting setting,
                        Action<ConfigurationOptions>? configurationHook = null)
    {
        var configuration = RedisClientFactory.CreateConfigurationOptions(configurationHook);
        return configuration.CreateRedisConsumerBuilder(setting);
    }

    /// <summary>
    /// Create REDIS consumer builder.
    /// </summary>
    /// <param name="setting">The setting.</param>
    /// <param name="endpoint">The raw endpoint (not an environment variable).</param>
    /// <param name="password">The password (not an environment variable).</param>
    /// <param name="configurationHook">The configuration hook.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder CreateRedisConsumerBuilder(
                        this RedisConsumerChannelSetting setting,
                        string endpoint,
                        string? password = null,
                        Action<ConfigurationOptions>? configurationHook = null)
    {
        var configuration = RedisClientFactory.CreateConfigurationOptions(endpoint, password, configurationHook);
        return configuration.CreateRedisConsumerBuilder(setting);
    }

    /// <summary>
    /// Create REDIS consumer builder.
    /// </summary>
    /// <param name="credentialsKeys">The credentials keys.</param>
    /// <param name="setting">The setting.</param>
    /// <param name="configurationHook">The configuration hook.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder CreateRedisConsumerBuilder(
                        this RedisCredentialsEnvKeys credentialsKeys,
                        RedisConsumerChannelSetting? setting = null,
                        Action<ConfigurationOptions>? configurationHook = null)
    {
        var configuration = credentialsKeys.CreateConfigurationOptions(configurationHook);
        return configuration.CreateRedisConsumerBuilder(setting);
    }

    #endregion // CreateRedisConsumerBuilder

    #region UseRedisChannel

    /// <summary>
    /// Uses REDIS consumer channel.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="setting">The setting.</param>
    /// <param name="redisConfiguration">The redis configuration.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder UseRedisChannel(
                        this IConsumerBuilder builder,
                        RedisConsumerChannelSetting? setting = null,
                        ConfigurationOptions? redisConfiguration = null)
    {
        var stg = setting ?? RedisConsumerChannelSetting.Default;
        var channelBuilder = builder.UseChannel(LocalCreate);
        return channelBuilder;

        IConsumerChannelProvider LocalCreate(ILogger logger)
        {
            var channel = new RedisConsumerChannel(
                                    logger,
                                    redisConfiguration,
                                    stg);
            return channel;
        }
    }

    /// <summary>
    /// Uses REDIS consumer channel.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="credentialsKeys">Environment keys of the credentials</param>
    /// <param name="setting">The setting.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder UseRedisChannel(
                        this IConsumerBuilder builder,
                        RedisCredentialsEnvKeys credentialsKeys,
                        RedisConsumerChannelSetting? setting = null)
    {
        var channelBuilder = builder.UseChannel(LocalCreate);
        return channelBuilder;

        IConsumerChannelProvider LocalCreate(ILogger logger)
        {
            var channel = new RedisConsumerChannel(
                                    logger,
                                    credentialsKeys,
                                    setting);
            return channel;
        }
    }

    /// <summary>
    /// Uses REDIS consumer channel.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="redisClientFactory">The redis client factory.</param>
    /// <param name="setting">The setting.</param>
    /// <returns></returns>
    internal static IConsumerStoreStrategyBuilder UseRedisChannel(
                        this IConsumerBuilder builder,
                        IEventSourceRedisConnectionFactory redisClientFactory,
                        RedisConsumerChannelSetting? setting = null)
    {
        var channelBuilder = builder.UseChannel(LocalCreate);
        return channelBuilder;

        IConsumerChannelProvider LocalCreate(ILogger logger)
        {
            var channel = new RedisConsumerChannel(
                                    redisClientFactory,
                                    logger,
                                    setting);
            return channel;
        }
    }

    /// <summary>
    /// Uses REDIS consumer channel.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="setting">The setting.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">redisClient</exception>
    public static IConsumerIocStoreStrategyBuilder ResolveRedisConsumerChannel(
                        this IConsumerBuilder builder,
                        IServiceProvider serviceProvider,
                        RedisConsumerChannelSetting? setting = null)
    {
        var channelBuilder = builder.UseChannel(serviceProvider, LocalCreate);
        return channelBuilder;

        IConsumerChannelProvider LocalCreate(ILogger logger)
        {
            var connFactory = serviceProvider.GetService<IEventSourceRedisConnectionFactory>();
            if (connFactory == null)
                throw new RedisConnectionException(ConnectionFailureType.None, $"{nameof(IEventSourceRedisConnectionFactory)} is not registered, use services.{nameof(RedisDiExtensions.AddEventSourceRedisConnection)} in order to register it at Setup stage.");
            var channel = new RedisConsumerChannel(
                                    connFactory,
                                    logger,
                                    setting);
            return channel;
        }
    }

    /// <summary>
    /// Uses REDIS consumer channel.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="setting">The setting.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">redisClient</exception>
    public static IConsumerIocStoreStrategyBuilder ResolveRedisConsumerChannel(
                        this IServiceProvider serviceProvider,
                        RedisConsumerChannelSetting? setting = null)
    {
        var result = ConsumerBuilder.Create(serviceProvider).ResolveRedisConsumerChannel(serviceProvider, setting);
        return result;
    }

    #endregion // UseRedisChannel

    #region AddRedisHashStorage

    /// <summary>
    /// Uses REDIS consumer storage.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="redisConfiguration">The redis configuration.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder AddRedisHashStorage(
                        this IConsumerStoreStrategyBuilder builder,
                        ConfigurationOptions? redisConfiguration = null)
    {
        var channelBuilder = builder.AddStorageStrategyFactory(LocalCreate);
        return channelBuilder;

        IConsumerStorageStrategy LocalCreate(ILogger logger)
        {
            var connFactory = EventSourceRedisConnectionFactory.Create(
                                                        logger,
                                                        redisConfiguration);
            if (connFactory == null)
                throw new RedisConnectionException(ConnectionFailureType.None, $"{nameof(IEventSourceRedisConnectionFactory)} is not registered, use services.{nameof(RedisDiExtensions.AddEventSourceRedisConnection)} in order to register it at Setup stage.");
            var storage = new RedisHashStorageStrategy(connFactory, logger);
            return storage;
        }
    }

    /// <summary>
    /// Uses REDIS consumer storage.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="credentialsKeys">Environment keys of the credentials</param>
    /// <param name="configurationHook">The configuration hook.</param>
    /// <returns></returns>
    public static IConsumerStoreStrategyBuilder AddRedisHashStorage(
                        this IConsumerStoreStrategyBuilder builder,
                        RedisCredentialsEnvKeys credentialsKeys,
                        Action<ConfigurationOptions>? configurationHook = null)
    {
        var configuration = credentialsKeys.CreateConfigurationOptions(configurationHook);
        var channelBuilder = builder.AddRedisHashStorage(configuration);
        return channelBuilder;
    }

    /// <summary>
    /// Uses REDIS consumer storage.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="redisClientFactory">The redis client factory.</param>
    /// <returns></returns>
    internal static IConsumerStoreStrategyBuilder AddRedisHashStorage(
                        this IConsumerStoreStrategyBuilder builder,
                        IEventSourceRedisConnectionFactory redisClientFactory)
    {
        var channelBuilder = builder.AddStorageStrategyFactory(LocalCreate);
        return channelBuilder;

        IConsumerStorageStrategy LocalCreate(ILogger logger)
        {
            var storage = new RedisHashStorageStrategy(redisClientFactory, logger);
            return storage;
        }
    }

    #endregion // AddRedisHashStorage

    #region ResolveRedisHashStorage

    /// <summary>
    /// Uses REDIS consumer storage.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentNullException">redisClient</exception>
    public static IConsumerIocStoreStrategyBuilder ResolveRedisHashStorage(
                        this IConsumerIocStoreStrategyBuilder builder)
    {
        var channelBuilder = builder.AddStorageStrategyFactory(LocalCreate);
        return channelBuilder;

        IConsumerStorageStrategy LocalCreate(ILogger logger)
        {
            IServiceProvider serviceProvider = builder.ServiceProvider;
            var connFactory = serviceProvider.GetService<IEventSourceRedisConnectionFactory>();
            if (connFactory == null)
                throw new RedisConnectionException(ConnectionFailureType.None, $"{nameof(IEventSourceRedisConnectionFactory)} is not registered, use services.{nameof(RedisDiExtensions.AddEventSourceRedisConnection)} in order to register it at Setup stage.");
            var storage = new RedisHashStorageStrategy(connFactory, logger);
            return storage;
        }
    }

    #endregion // ResolveRedisHashStorage
}
