using StackExchange.Redis;

namespace EventSourcing.Backbone
{
    /// <summary>
    /// Connection factory
    /// </summary>
    public interface IEventSourceRedisConnectionFactory
    {
        /// <summary>
        /// Get a valid connection
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task<IConnectionMultiplexer> GetAsync(CancellationToken cancellationToken);
        /// <summary>
        /// Get database
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task<IDatabaseAsync> GetDatabaseAsync(CancellationToken cancellationToken);
    }
}