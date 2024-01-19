using System.Collections.Immutable;

using EventSourcing.Backbone.Channels.RedisProvider.Common;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace EventSourcing.Backbone.Channels
{
    /// <summary>
    /// Responsible of saving information to a storage.
    /// The information can be either Segmentation or Interception.
    /// </summary>
    internal class RedisProducerHashStorageStrategy : ProducerStorageStrategyBase
    {
        private readonly IEventSourceRedisConnectionFactory _connFactory;

        #region Ctor

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="connFactory">The connection factory.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="behavior">
        /// Define the storage behavior
        /// Useful when having multi storage configuration.
        /// May use to implement storage splitting (separation of concerns) like in the case of GDPR.
        /// </param>
        public RedisProducerHashStorageStrategy(
                        IEventSourceRedisConnectionFactory connFactory,
                        ILogger logger,
                        StorageBehavior? behavior = null)
                            : base(logger, behavior)
        {
            _connFactory = connFactory;
        }

        #endregion // Ctor

        #region Name

        /// <summary>
        /// Gets the name of the storage provider.
        /// </summary>
        public override string Name { get; } = "Redis";

        #endregion // Name

        #region OnSaveBucketAsync

        /// <summary>
        /// Saves the bucket information.
        /// </summary>
        /// <param name="bucket">Either Segments or Interceptions (after filtering).</param>
        /// <param name="type">The type.</param>
        /// <param name="meta">The metadata.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns>
        /// Array of metadata entries which can be used by the consumer side storage strategy, in order to fetch the data.
        /// </returns>
        protected override async ValueTask<IImmutableDictionary<string, string>> OnSaveBucketAsync(
                                                                    IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>> bucket,
                                                                    EventBucketCategories type,
                                                                    Metadata meta,
                                                                    CancellationToken cancellation)
        {
            var conn = await _connFactory.GetAsync(cancellation);

            IDatabaseAsync db = conn.GetDatabase();

            var segmentsEntities = bucket.Select(sgm =>
                                                    new HashEntry(sgm.Key, sgm.Value))
                                        .ToArray();
            var signature = meta.Signature;
            string key = $"{meta.FullUri()}:{signature}:{type}:{meta.MessageId:N}";
            await db.HashSetAsync(key, segmentsEntities);
            if (_timeToLive != null)
                await db.KeyExpireAsync(key, _timeToLive);

            string propKey = $"{RedisChannelConstants.STORE_KEY}:{type}";
            return ImmutableDictionary<string, string>.Empty.Add(propKey, key);
        }

        #endregion // OnSaveBucketAsync
    }
}
