using EventSourcing.Backbone.Channels.RedisProvider.Common;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace EventSourcing.Backbone.Channels;

/// <summary>
/// Responsible to save information to REDIS hash storage.
/// The information can be either Segmentation or Interception.
/// When adding it via the builder it can be arrange in a chain in order of having
/// 'Chain of Responsibility' for saving different parts into different storage (For example GDPR's PII).
/// Alternative, chain can serve as a cache layer.
/// </summary>
internal class RedisHashStorageStrategy : IConsumerStorageStrategy
{
    private readonly IEventSourceRedisConnectionFactory _connFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connFactory">The database task.</param>
    /// <param name="logger">The logger.</param>
    public RedisHashStorageStrategy(
        IEventSourceRedisConnectionFactory connFactory,
        ILogger logger)
    {
        _connFactory = connFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the name of the storage provider.
    /// </summary>
    public string Name { get; } = "Redis";

    /// <summary>
    /// Load the bucket information.
    /// </summary>
    /// <param name="meta">The meta fetch provider.</param>
    /// <param name="prevBucket">The current bucket (previous item in the chain).</param>
    /// <param name="type">The type of the storage.</param>
    /// <param name="getProperty">The get property.</param>
    /// <param name="cancellation">The cancellation.</param>
    /// <returns>
    /// Either Segments or Interceptions.
    /// </returns>
    /// <exception cref="System.NotImplementedException"></exception>
    async ValueTask<Bucket> IConsumerStorageStrategy.LoadBucketAsync(
        Metadata meta,
        Bucket prevBucket,
        EventBucketCategories type,
        Func<string, string> getProperty,
        CancellationToken cancellation)
    {
        string propKey = $"{RedisChannelConstants.STORE_KEY}:{type}";
        string key = getProperty(propKey);

        IConnectionMultiplexer conn = await _connFactory.GetAsync(cancellation);
        IDatabaseAsync db = conn.GetDatabase();
        HashEntry[] entities;
        try
        {
            entities = await db.HashGetAllAsync(key, CommandFlags.DemandMaster); // DemandMaster avoid racing
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.FormatLazy(), "Fail to load from redis storage [{type}]", type);
            throw;
        }

#pragma warning disable CS8600 
#pragma warning disable CS8620
        var pairs = entities
                        .Select(m => (Key: (string)m.Name, Value: (byte[])m.Value));
        var results = prevBucket.TryAddRange(pairs);
#pragma warning restore CS8620
#pragma warning restore CS8600

        return results;

    }

}
