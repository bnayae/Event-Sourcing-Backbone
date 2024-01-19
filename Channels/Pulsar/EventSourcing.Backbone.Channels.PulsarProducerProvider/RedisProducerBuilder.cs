using EventSourcing.Backbone.Channels.RedisProvider;
using EventSourcing.Backbone.Private;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Trace;

using Polly;

using StackExchange.Redis;

namespace EventSourcing.Backbone
{
    public static class RedisProducerBuilder
    {
        /// <summary>
        /// Adds the event producer telemetry source (will result in tracing the producer).
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <returns></returns>
        public static TracerProviderBuilder AddEventProducerTelemetry(this TracerProviderBuilder builder) => builder.AddSource(nameof(RedisProducerChannel));

        /// <summary>
        /// Uses REDIS producer channel.
        /// </summary>
        /// <param name="resiliencePolicy">The resilience policy.</param>
        /// <param name="configurationHook">The configuration hook.</param>
        /// <returns></returns>
        public static IProducerStoreStrategyBuilder Create(
                            AsyncPolicy? resiliencePolicy = null,
                            Action<ConfigurationOptions>? configurationHook = null)
        {
            var configuration = RedisClientFactory.CreateConfigurationOptions(configurationHook);
            return CreateRedisProducerBuilder(configuration, resiliencePolicy);
        }

        /// <summary>
        /// Uses REDIS producer channel.
        /// </summary>
        /// <param name="endpoint">
        /// Environment key of the end-point, if missing it use a default ('REDIS_EVENT_SOURCE_ENDPOINT').
        /// If the environment variable doesn't exists, It assumed that the value represent an actual end-point and use it.
        /// </param>
        /// <param name="password">
        /// Environment key of the password, if missing it use a default ('REDIS_EVENT_SOURCE_PASS').
        /// If the environment variable doesn't exists, It assumed that the value represent an actual password and use it.
        /// </param>
        /// <param name="resiliencePolicy">The resilience policy.</param>
        /// <param name="configurationHook">The configuration hook.</param>
        /// <returns></returns>
        public static IProducerStoreStrategyBuilder Create(
                            string endpoint,
                            string? password = null,
                            AsyncPolicy? resiliencePolicy = null,
                            Action<ConfigurationOptions>? configurationHook = null)
        {
            var configuration = RedisClientFactory.CreateConfigurationOptions(endpoint, password, configurationHook);
            return CreateRedisProducerBuilder(configuration, resiliencePolicy);
        }

        /// <summary>
        /// Uses REDIS producer channel.
        /// </summary>
        /// <param name="credential">The credential.</param>
        /// <param name="resiliencePolicy">The resilience policy.</param>
        /// <param name="configurationHook">The configuration hook.</param>
        /// <returns></returns>
        public static IProducerStoreStrategyBuilder CreateRedisProducerBuilder(
                            this IRedisCredentials credential,
                            AsyncPolicy? resiliencePolicy = null,
                            Action<ConfigurationOptions>? configurationHook = null)
        {
            var configuration = credential.CreateConfigurationOptions(configurationHook);
            return CreateRedisProducerBuilder(configuration, resiliencePolicy);
        }

        /// <summary>
        /// Uses REDIS producer channel.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="resiliencePolicy">The resilience policy.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static IProducerStoreStrategyBuilder CreateRedisProducerBuilder(
                            this ConfigurationOptions configuration,
                            AsyncPolicy? resiliencePolicy = null)
        {
            var builder = ProducerBuilder.Empty;

            return builder.UseRedisChannel(configuration, resiliencePolicy);
        }

        /// <summary>
        /// Uses REDIS producer channel.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="resiliencePolicy">The resilience policy.</param>
        /// <returns></returns>
        public static IProducerStoreStrategyBuilder UseRedisChannel(
                            this IProducerBuilder builder,
                            ConfigurationOptions? configuration = null,
                            AsyncPolicy? resiliencePolicy = null)
        {
            var result = builder.UseChannel(LocalCreate);
            return result;

            IProducerChannelProvider LocalCreate(ILogger logger)
            {
                var connFactory = EventSourceRedisConnectionFactory.Create(logger, configuration);
                var channel = new RedisProducerChannel(
                                    connFactory,
                                    logger ?? EventSourceFallbakLogger.Default,
                                    resiliencePolicy);
                return channel;
            }
        }

        /// <summary>
        /// Uses REDIS producer channel.
        /// This overload is used by the DI
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="redisConnectionFactory">The redis database.</param>
        /// <param name="resiliencePolicy">The resilience policy.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        internal static IProducerStoreStrategyBuilder UseRedisChannel(
                            this IProducerBuilder builder,
                            IEventSourceRedisConnectionFactory redisConnectionFactory,
                            AsyncPolicy? resiliencePolicy = null)
        {
            var result = builder.UseChannel(LocalCreate);
            return result;

            IProducerChannelProvider LocalCreate(ILogger logger)
            {
                var channel = new RedisProducerChannel(
                                 redisConnectionFactory,
                                 logger ?? EventSourceFallbakLogger.Default,
                                 resiliencePolicy);
                return channel;
            }
        }

        /// <summary>
        /// Uses REDIS producer channel by resolving it as a dependency injection from the service-provider.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="resiliencePolicy">The resilience policy.</param>
        /// <returns></returns>
        public static IProducerIocStoreStrategyBuilder ResolveRedisProducerChannel(
                            this IProducerBuilder builder,
                            IServiceProvider serviceProvider,
                            AsyncPolicy? resiliencePolicy = null)
        {
            var result = builder.UseChannel(serviceProvider, LocalCreate);
            return result;

            IProducerChannelProvider LocalCreate(ILogger logger)
            {
                var connFactory = serviceProvider.GetService<IEventSourceRedisConnectionFactory>();
                if (connFactory == null)
                    throw new RedisConnectionException(ConnectionFailureType.None, $"{nameof(IEventSourceRedisConnectionFactory)} is not registered, use services.{nameof(RedisDiExtensions.AddEventSourceRedisConnection)} in order to register it at Setup stage.");
                var channel = new RedisProducerChannel(
                                 connFactory,
                                 logger ?? EventSourceFallbakLogger.Default,
                                 resiliencePolicy);
                return channel;
            }
        }

        /// <summary>
        /// Uses REDIS producer channel by resolving it as a dependency injection from the service-provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="resiliencePolicy">The resilience policy.</param>
        /// <returns></returns>
        public static IProducerIocStoreStrategyBuilder ResolveRedisProducerChannel(
                            this IServiceProvider serviceProvider,
                            AsyncPolicy? resiliencePolicy = null)
        {
            var result = ProducerBuilder.Create(serviceProvider).ResolveRedisProducerChannel(serviceProvider, resiliencePolicy);
            return result;
        }
    }
}
