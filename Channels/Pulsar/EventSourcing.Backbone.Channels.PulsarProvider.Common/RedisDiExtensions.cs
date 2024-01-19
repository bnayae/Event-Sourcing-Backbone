using EventSourcing.Backbone;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using static EventSourcing.Backbone.Channels.RedisProvider.Common.RedisChannelConstants;

namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// The redis DI extensions.
    /// </summary>
    public static class RedisDiExtensions
    {
        /// <summary>
        /// Adds the event source redis connection to the DI.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <returns>An IServiceCollection.</returns>
        public static IServiceCollection AddEventSourceRedisConnection(
            this IServiceCollection services)
        {
            services.AddSingleton<IEventSourceRedisConnectionFactory, EventSourceRedisConnectionFactory>();

            return services;
        }

        /// <summary>
        /// Adds the event source redis connection to the DI.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="endpoint">The raw endpoint (not an environment variable).</param>
        /// <param name="password">The password (not an environment variable).</param>
        /// <returns>
        /// An IServiceCollection.
        /// </returns>
        public static IServiceCollection AddEventSourceRedisConnection(
            this IServiceCollection services,
                    string endpoint,
                    string? password = null)
        {
            services.AddSingleton<IEventSourceRedisConnectionFactory>(
                sp =>
                {
                    ILogger logger = sp.GetService<ILogger<EventSourceRedisConnectionFactory>>() ??
                                            throw new EventSourcingException(
                                                $"{nameof(AddEventSourceRedisConnection)}: Cannot resolve a logger");

                    var factory = EventSourceRedisConnectionFactory.Create(logger, endpoint, password);
                    return factory;
                });

            return services;
        }

        /// <summary>
        /// Adds the event source redis connection to the DI.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="endpointEnvKey">The environment variable key of the endpoint.</param>
        /// <param name="passwordEnvKey">The environment variable key of the password.</param>
        /// <returns>
        /// An IServiceCollection.
        /// </returns>
        public static IServiceCollection AddEventSourceRedisConnectionFromEnv(
            this IServiceCollection services,
                    string endpointEnvKey,
                    string passwordEnvKey = PASSWORD_KEY)
        {
            services.AddSingleton<IEventSourceRedisConnectionFactory>(
                sp =>
                {
                    ILogger logger = sp.GetService<ILogger<EventSourceRedisConnectionFactory>>() ??
                                            throw new EventSourcingException(
                                                $"{nameof(AddEventSourceRedisConnectionFromEnv)}: Cannot resolve a logger");

                    var factory = EventSourceRedisConnectionFactory.CreateFromEnv(logger, endpointEnvKey, passwordEnvKey);
                    return factory;
                });

            return services;
        }
    }
}
