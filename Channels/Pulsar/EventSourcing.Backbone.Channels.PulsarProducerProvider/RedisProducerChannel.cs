using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

using EventSourcing.Backbone.Producers;

using Microsoft.Extensions.Logging;

using Polly;

using StackExchange.Redis;

using static EventSourcing.Backbone.Channels.RedisProvider.Common.RedisChannelConstants;

namespace EventSourcing.Backbone.Channels.RedisProvider;

/// <summary>
/// Redis producer channel provider
/// </summary>
internal class RedisProducerChannel : ProducerChannelBase
{
    private readonly IEventSourceRedisConnectionFactory _connFactory;
    private const string META_SLOT = "__<META>__";

    #region Ctor

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="redisFactory">The redis database promise.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="resiliencePolicy">The resilience policy for retry.</param>
    public RedisProducerChannel(
                    IEventSourceRedisConnectionFactory redisFactory,
                    ILogger logger,
                    AsyncPolicy? resiliencePolicy) : base(logger, resiliencePolicy)
    {
        _connFactory = redisFactory;
        DefaultStorageStrategy = new RedisProducerHashStorageStrategy(_connFactory, logger);
    }


    #endregion // Ctor

    #region DefaultStorageStrategy

    /// <summary>
    /// Gets the default storage strategy.
    /// </summary>
    protected override IProducerStorageStrategy DefaultStorageStrategy { get; }

    #endregion // DefaultStorageStrategy

    #region ChannelType

    /// <summary>
    /// Gets the type of the channel.
    /// </summary>
    protected override string ChannelType { get; } = CHANNEL_TYPE;

    #endregion // ChannelType

    #region SendAsync

    /// <summary>
    /// Sends announcement.
    /// Happens right after saving into the storage
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="payload">The raw announcement data.</param>
    /// <param name="storageMeta">The storage meta information, like the bucket name in case of s3, etc.</param>
    /// <returns>
    /// Return the message id
    /// </returns>
    protected async override ValueTask<string> OnSendAsync(
                IProducerPlan plan,
                Announcement payload,
                IImmutableDictionary<string, string> storageMeta)
    {
        Metadata meta = payload.Metadata;
        string env = meta.Environment.ToDash();
        string uri = meta.UriDash;

        #region var entries = ...

        string metaJson = JsonSerializer.Serialize(meta, EventSourceOptions.SerializerOptions);

        // local method 
        NameValueEntry KV(RedisValue key, RedisValue value) => new NameValueEntry(key, value);
        ImmutableArray<NameValueEntry> commonEntries = ImmutableArray.Create(
            KV(nameof(meta.ProducedAt), meta.ProducedAt.ToUnixTimeSeconds()),
            KV(META_SLOT, metaJson)
        );

        var telemetryBuilder = commonEntries.ToBuilder();
        foreach (var item in storageMeta)
        {
            var kv = KV(item.Key, item.Value);
            telemetryBuilder.Add(kv);
            commonEntries = commonEntries.Add(kv);
        }
        Activity.Current?.InjectSpan(telemetryBuilder, LocalInjectTelemetry);
        var entries = telemetryBuilder.ToArray();

        #endregion // var entries = ...

        try
        {
            IConnectionMultiplexer conn = await _connFactory.GetAsync(CancellationToken.None);
            IDatabaseAsync db = conn.GetDatabase();
            var k = meta.FullUri();
            var result = await db.StreamAddAsync(k, entries,
                                           flags: CommandFlags.DemandMaster);
            return (string)result!;
        }
        #region Exception Handling

        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "REDIS Connection Failure: push event [{id}] into the [{env}:{URI}] stream: {operation}",
                meta.MessageId, env, uri, meta.Signature);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to push event [{id}] into the [{env}:{URI}] stream: {operation}",
                meta.MessageId, env, uri, meta.Signature);
            throw;
        }

        #endregion // Exception Handling

        #region LocalInjectTelemetry

        void LocalInjectTelemetry(
                        ImmutableArray<NameValueEntry>.Builder builder,
                        string key,
                        string value)
        {
            builder.Add(KV(key, value));
        }

        #endregion // LocalInjectTelemetry

    }

    #endregion // SendAsync
}
