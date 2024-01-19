using EventSourcing.Backbone.Channels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace EventSourcing.Backbone;

/// <summary>
/// Extension methods for S3 storage strategy.
/// </summary>
public static class RedisHashProducerStorageStrategyExtension
{
    #region AddRedisHashStorage

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="filterByOperationAndKey">
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR .
    /// The predicate signature is: (metadata, key) => bool
    ///   the key is driven from the method parameter.
    /// </param>
    /// <returns></returns>
    public static IProducerStoreStrategyBuilder AddRedisHashStorage(
            this IProducerStoreStrategyBuilder builder,
            Func<Metadata, string, bool> filterByOperationAndKey)
    {

        var result = builder.AddRedisHashStorage((StorageBehavior)filterByOperationAndKey);
        return result;
    }

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="behavior">
    /// Define the storage behavior
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR.
    /// </param>
    /// <returns></returns>
    public static IProducerStoreStrategyBuilder AddRedisHashStorage(
            this IProducerStoreStrategyBuilder builder,
            StorageBehavior? behavior = null)
    {

        var result = builder.AddStorageStrategy(Local);
        return result;
        IProducerStorageStrategy Local(ILogger logger)
        {
            var connFactory = EventSourceRedisConnectionFactory.Create(logger);
            var storage = new RedisProducerHashStorageStrategy(connFactory, logger, behavior);
            return storage;
        }
    }

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="credential">The credential.</param>
    /// <param name="filterByOperationAndKey">
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR .
    /// The predicate signature is: (metadata, key) => bool
    ///   the key is driven from the method parameter.
    /// </param>
    /// <param name="configurationHook">The configuration hook.</param>
    /// <returns></returns>
    public static IProducerStoreStrategyBuilder AddRedisHashStorage(
            this IProducerStoreStrategyBuilder builder,
            IRedisCredentials credential,
            Func<Metadata, string, bool> filterByOperationAndKey,
            Action<ConfigurationOptions>? configurationHook = null)
    {
        var result = builder.AddRedisHashStorage(credential, (StorageBehavior)filterByOperationAndKey, configurationHook);
        return result;
    }

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="credential">The credential.</param>
    /// <param name="behavior">
    /// Define the storage behavior
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR.
    /// </param>
    /// <param name="configurationHook">The configuration hook.</param>
    /// <returns></returns>
    public static IProducerStoreStrategyBuilder AddRedisHashStorage(
            this IProducerStoreStrategyBuilder builder,
            IRedisCredentials credential,
            StorageBehavior? behavior = null,
            Action<ConfigurationOptions>? configurationHook = null)
    {

        var result = builder.AddStorageStrategy(Local);
        return result;

        IProducerStorageStrategy Local(ILogger logger)
        {
            var configuration = credential.CreateConfigurationOptions(configurationHook);
            var connFactory = EventSourceRedisConnectionFactory.Create(logger, configuration);
            var storage = new RedisProducerHashStorageStrategy(connFactory, logger, behavior);
            return storage;
        }
    }

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="filterByOperationAndKey">
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR .
    /// The predicate signature is: (metadata, key) => bool
    ///   the key is driven from the method parameter.
    /// </param>
    /// <returns></returns>
    public static IProducerStoreStrategyBuilder AddRedisHashStorage(
            this IProducerStoreStrategyBuilder builder,
            ConfigurationOptions configuration,
            Func<Metadata, string, bool> filterByOperationAndKey)
    {
        var result = builder.AddRedisHashStorage(configuration, (StorageBehavior)filterByOperationAndKey);
        return result;
    }

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="behavior">
    /// Define the storage behavior
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR.
    /// </param>
    /// <returns></returns>
    public static IProducerStoreStrategyBuilder AddRedisHashStorage(
            this IProducerStoreStrategyBuilder builder,
            ConfigurationOptions configuration,
            StorageBehavior? behavior = null)
    {

        var result = builder.AddStorageStrategy(Local);
        return result;

        IProducerStorageStrategy Local(ILogger logger)
        {
            var connFactory = EventSourceRedisConnectionFactory.Create(logger, configuration);
            var storage = new RedisProducerHashStorageStrategy(connFactory, logger, behavior);
            return storage;
        }
    }

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="connFactory">The redis connection factory.</param>
    /// <param name="behavior">
    /// Define the storage behavior
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR.
    /// </param>
    /// <returns></returns>
    public static IProducerStoreStrategyBuilder AddRedisHashStorage(
        this IProducerStoreStrategyBuilder builder,
        IEventSourceRedisConnectionFactory connFactory,
        StorageBehavior? behavior = null)
    {

        var result = builder.AddStorageStrategy(Local);
        return result;

        IProducerStorageStrategy Local(ILogger logger)
        {
            var storage = new RedisProducerHashStorageStrategy(connFactory, logger, behavior);
            return storage;
        }
    }

    #endregion // AddRedisHashStorage

    #region ResolveRedisHashStorage

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="filterByOperationAndKey">
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR .
    /// The predicate signature is: (metadata, key) => bool
    ///   the key is driven from the method parameter.
    /// </param>
    /// <returns></returns>
    public static IProducerIocStoreStrategyBuilder ResolveRedisHashStorage(
            this IProducerIocStoreStrategyBuilder builder,
            Func<Metadata, string, bool> filterByOperationAndKey)
    {
        var result = builder.ResolveRedisHashStorage((StorageBehavior)filterByOperationAndKey);
        return result;
    }

    /// <summary>
    /// Resolves the redis hash.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="behavior">
    /// Define the storage behavior
    /// Useful when having multi storage configuration.
    /// May use to implement storage splitting (separation of concerns) like in the case of GDPR.
    /// </param>
    /// <returns></returns>
    public static IProducerIocStoreStrategyBuilder ResolveRedisHashStorage(
            this IProducerIocStoreStrategyBuilder builder,
            StorageBehavior? behavior = null)
    {

        var result = builder.AddStorageStrategy(Local);
        return result;

        IProducerStorageStrategy Local(ILogger logger)
        {
            IEventSourceRedisConnectionFactory? connFactory = builder.ServiceProvider.GetService<IEventSourceRedisConnectionFactory>();
            if (connFactory == null)
            {
                string error = $"Redis Hash Storage: {nameof(IEventSourceRedisConnectionFactory)} is not registered as dependency injection";
                logger.LogError(error);
                throw new EventSourcingException(error);
            }
            var storage = new RedisProducerHashStorageStrategy(connFactory, logger, behavior);
            return storage;
        }
    }

    #endregion // ResolveRedisHashStorage
}
