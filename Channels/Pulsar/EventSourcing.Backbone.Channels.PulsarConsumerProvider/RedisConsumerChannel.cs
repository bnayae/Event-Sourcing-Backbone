using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using EventSourcing.Backbone.Building;
using EventSourcing.Backbone.Channels.RedisProvider.Common;
using EventSourcing.Backbone.Consumers;
using EventSourcing.Backbone.Private;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using static System.Math;
using static EventSourcing.Backbone.Private.EventSourceTelemetry;

// TODO: [bnaya 2021-07] MOVE TELEMETRY TO THE BASE CLASSES OF PRODUCER / CONSUME

namespace EventSourcing.Backbone.Channels.RedisProvider;

/// <summary>
/// The redis consumer channel.
/// </summary>
internal class RedisConsumerChannel : ConsumerChannelBase, IConsumerChannelProvider
{
    private static readonly Counter<int> StealCountCounter = EMeter.CreateCounter<int>("evt-src.sys.consumer.events-stealing", "count",
                                                                                "Attempt to get stale events (messages) from other consumer (which assumed malfunction)");
    private static readonly Counter<int> StealAmountCounter = EMeter.CreateCounter<int>("evt-src.sys.consumer.events-stealing.messages", "count",
                                                                                "Attempt to get stale events (messages) from other consumer (which assumed malfunction)");
    private static readonly Counter<int> ConcumeBatchCountCounter = EMeter.CreateCounter<int>("evt-src.sys.consumer.batch", "count",
                                                "count of the number of non empty consuming batches form the stream provider");
    private static readonly Counter<int> ConcumeEventsCounter = EMeter.CreateCounter<int>("evt-src.sys.consumer.events", "count",
                                                "Sum of total consuming events (messages) before process");
    private static readonly Counter<int> ConcumeEventsOperationCounter = EMeter.CreateCounter<int>("evt-src.sys.consumer.events.operation", "count",
                                                "Sum of total consuming events (messages) before process");
    private static readonly Counter<int> ConcumeBatchFailureCounter = EMeter.CreateCounter<int>("evt-src.sys.consumer.batch.failure", "count",
                                                "batch reading failure");

    private const string BEGIN_OF_STREAM = "0000000000000";
    /// <summary>
    /// The read by identifier chunk size.
    /// REDIS don't have option to read direct position (it read from a position, not includes the position itself),
    /// therefore read should start before the actual position.
    /// </summary>
    private const int READ_BY_ID_CHUNK_SIZE = 10;
    /// <summary>
    /// Receiver max iterations
    /// </summary>
    private const int READ_BY_ID_ITERATIONS = 1000 / READ_BY_ID_CHUNK_SIZE;

    private readonly RedisConsumerChannelSetting _setting;
    private readonly IEventSourceRedisConnectionFactory _connFactory;
    private readonly IConsumerStorageStrategy _defaultStorageStrategy;
    private const string META_SLOT = "__<META>__";
    private const int INIT_RELEASE_DELAY = 100;
    private const int MAX_RELEASE_DELAY = 1000 * 30; // 30 seconds

    #region Ctor

    #region Overloads

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="setting">The setting.</param>
    public RedisConsumerChannel(
                    ILogger logger,
                    ConfigurationOptions? configuration = null,
                    RedisConsumerChannelSetting? setting = null) : this(
                         EventSourceRedisConnectionFactory.Create(
                                                logger,
                                                configuration),
                        logger,
                        setting)
    {
    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="credentialsKeys">Environment keys of the credentials</param>
    /// <param name="setting">The setting.</param>
    /// <param name="configurationHook">The configuration hook.</param>
    public RedisConsumerChannel(
                    ILogger logger,
                    IRedisCredentials credentialsKeys,
                    RedisConsumerChannelSetting? setting = null,
                    Action<ConfigurationOptions>? configurationHook = null) : this(
                        EventSourceRedisConnectionFactory.Create(
                                                credentialsKeys,
                                                logger,
                                                configurationHook),
                        logger,
                        setting)
    {
    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="endpoint">The raw endpoint (not an environment variable).</param>
    /// <param name="password">The password (not an environment variable).</param>
    /// <param name="setting">The setting.</param>
    /// <param name="configurationHook">The configuration hook.</param>
    public RedisConsumerChannel(
                    ILogger logger,
                    string endpoint,
                    string? password = null,
                    RedisConsumerChannelSetting? setting = null,
                    Action<ConfigurationOptions>? configurationHook = null) : this(
                        EventSourceRedisConnectionFactory.Create(
                                                logger,
                                                endpoint,
                                                password,
                                                configurationHook),
                        logger,
                        setting)
    {
    }

    #endregion // Overloads

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="redisConnFactory">The redis provider promise.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="setting">The setting.</param>
    public RedisConsumerChannel(
                    IEventSourceRedisConnectionFactory redisConnFactory,
                    ILogger logger,
                    RedisConsumerChannelSetting? setting = null)
        : base(logger)
    {
        _connFactory = redisConnFactory;
        _defaultStorageStrategy = new RedisHashStorageStrategy(redisConnFactory, logger);
        _setting = setting ?? RedisConsumerChannelSetting.Default;
    }

    #endregion // Ctor

    #region SubsribeToSingleAsync

    /// <summary>
    /// Subscribe to specific shard.
    /// </summary>
    /// <param name="plan">The consumer plan.</param>
    /// <param name="func">The function.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected override async Task OnSubscribeToSingleAsync(
                IConsumerPlan plan,
                Func<Announcement, IAck, ValueTask<bool>> func,
                ConsumerOptions options,
                CancellationToken cancellationToken)
    {
        var claimingTrigger = options.ClaimingTrigger;
        var minIdleTime = (int)options.ClaimingTrigger.MinIdleTime.TotalMilliseconds;

        var env = plan.Environment.ToDash();
        string uri = plan.Uri.ToDash();
        string fullUri = plan.FullUri();
        string consumerGroup = plan.ConsumerGroup.ToDash();

        bool isFirstBatchOrFailure = true;

        CommandFlags flags = CommandFlags.None;
        string? fetchUntil = options.FetchUntilUnixDateOrEmpty?.ToString();

        ILogger logger = plan.Logger ?? _logger;

        #region await CreateConsumerGroupIfNotExistsAsync(...)

        await _connFactory.CreateConsumerGroupIfNotExistsAsync(
            plan,
            RedisChannelConstants.NONE_CONSUMER,
            logger,
            cancellationToken);

        await _connFactory.CreateConsumerGroupIfNotExistsAsync(
            plan,
            plan.ConsumerGroup,
            logger,
            cancellationToken);

        #endregion // await CreateConsumerGroupIfNotExistsAsync(...)

        int releaseDelay = INIT_RELEASE_DELAY;
        int bachSize = options.BatchSize;

        TimeSpan delay = TimeSpan.Zero;
        int emptyBatchCount = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var proceed = await HandleBatchAsync();
            if (!proceed)
                break;
        }

        #region HandleBatchAsync

        // Handle single batch
        async ValueTask<bool> HandleBatchAsync()
        {
            var policy = _setting.Policy.Policy;
            return await policy.ExecuteAsync(HandleBatchBreakerAsync, cancellationToken);
        }

        #endregion // HandleBatchAsync

        #region HandleBatchBreakerAsync

        async Task<bool> HandleBatchBreakerAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            StreamEntry[] results = await ReadBatchAsync();
            emptyBatchCount = results.Length == 0 ? emptyBatchCount + 1 : 0;
            results = await ClaimStaleMessages(emptyBatchCount, results, ct);

            if (results.Length == 0)
            {
                if (fetchUntil == null)
                    delay = await DelayIfEmpty(plan, delay, cancellationToken);
                return fetchUntil == null;
            }

            ct.ThrowIfCancellationRequested();

            try
            {
                var batchCancellation = new CancellationTokenSource();
                int i = 0;
                batchCancellation.Token.Register(async () =>
                {
                    // TODO: [bnaya 2023-06-19 #RELEASE] committed id should be captured
                    RedisValue[] freeTargets = results[i..].Select(m => m.Id).ToArray();
                    await ReleaseAsync(freeTargets);
                });
                // TODO: [bnaya 2023-06-19] enable parallel consuming (when order doesn't matters) See #RELEASE
                for (; i < results.Length && !batchCancellation.IsCancellationRequested; i++)
                {
                    StreamEntry result = results[i];

                    Metadata meta = ExtractMetadata(result, out var channelMeta);

                    ActivityContext parentContext = EventSourceTelemetryExtensions.ExtractSpan(channelMeta, ExtractTraceContext);
                    using var activity = plan.StartConsumerTrace(meta, parentContext);

                    #region IEnumerable<string> ExtractTraceContext(Dictionary<RedisValue, RedisValue> entries, string key)

                    IEnumerable<string> ExtractTraceContext(Dictionary<RedisValue, RedisValue> entries, string key)
                    {
                        try
                        {
                            if (entries.TryGetValue(key, out var value))
                            {
                                if (string.IsNullOrEmpty(value))
                                    return Array.Empty<string>();
                                return new[] { value.ToString() };
                            }
                        }
                        #region Exception Handling

                        catch (Exception ex)
                        {
                            Exception err = ex.FormatLazy();
                            _logger.LogError(err, "Failed to extract trace context: {error}", err);
                        }

                        #endregion // Exception Handling

                        return Enumerable.Empty<string>();
                    }

                    #endregion // IEnumerable<string> ExtractTraceContext(Dictionary<RedisValue, RedisValue> entries, string key)

                    var ack = new AckOnce(
                                    fullUri,
                                    async (cause) =>
                                    {
                                        Activity.Current?.AddEvent("consumer.event.ack",
                                            t => PrepareTrace(t).Add("cause", cause));
                                        await AckAsync(result.Id);
                                    },
                                    plan.Options.AckBehavior, logger,
                                    (cause) =>
                                    {
                                        Activity.Current?.AddEvent("consumer.event.cancel",
                                                                    t => PrepareTrace(t).Add("cause", cause));
                                        batchCancellation.CancelSafe(); // cancel forward
                                        return ValueTask.CompletedTask;
                                    });

                    #region OriginFilter

                    MessageOrigin originFilter = plan.Options.OriginFilter;
                    if (originFilter != MessageOrigin.None && (originFilter & meta.Origin) == MessageOrigin.None)
                    {
                        Ack.Set(ack);
                        #region Log

                        _logger.LogInformation("Event Source skip consuming of event [{event-key}] because if origin is [{origin}] while the origin filter is sets to [{origin-filter}], Operation:[{operation}], Stream:[{stream}]", meta.EventKey, meta.Origin, originFilter, meta.Signature, meta.FullUri());

                        #endregion // Log
                        continue;
                    }

                    #endregion // OriginFilter

                    #region var announcement = new Announcement(...)

                    (Bucket segmets, Bucket interceptions) = await GetStorageAsync(plan, channelMeta, meta);

                    var announcement = new Announcement
                    {
                        Metadata = meta,
                        Segments = segmets,
                        InterceptorsData = interceptions
                    };

                    #endregion // var announcement = new Announcement(...)

                    bool succeed;
                    ConcumeEventsOperationCounter.WithEnvUriOperation(meta).Add(1);
                    Activity? execActivity = null;
                    if (ETracer.HasListeners())
                    {
                        var signature = meta.Signature;
                        execActivity = plan.StartTraceDebug($"consumer.{signature.Operation.ToDash()}.invoke", metadata: meta);
                    }
                    using (execActivity)
                    {
                        succeed = await func(announcement, ack);
                        execActivity?.SetTag("succeed", succeed);
                    }
                    if (succeed)
                    {
                        releaseDelay = INIT_RELEASE_DELAY;
                        bachSize = options.BatchSize;
                    }
                    else
                    {
                        // TODO: [bnaya 2023-06-19 #RELEASE] committed id should be captured
                        if (options.PartialBehavior == Enums.PartialConsumerBehavior.Sequential)
                        {
                            using (plan.StartTraceWarning("consumer.release-events-on-failure", metadata: meta))
                            {
                                RedisValue[] freeTargets = results[i..].Select(m => m.Id).ToArray();
                                await ReleaseAsync(freeTargets); // release the rest of the batch which doesn't processed yet                                                                 
                            }
                        }
                    }
                }
            }
            catch
            {
                isFirstBatchOrFailure = true;
            }
            return true;
        }

        #endregion // HandleBatchBreakerAsync

        #region ReadBatchAsync

        // read batch entities from REDIS
        async Task<StreamEntry[]> ReadBatchAsync()
        {
            // TBD: circuit-breaker
            try
            {
                var r = await _setting.Policy.Policy.ExecuteAsync(async (ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    StreamEntry[] values = Array.Empty<StreamEntry>();
                    values = await ReadSelfPending();

                    if (values.Length == 0)
                    {
                        isFirstBatchOrFailure = false;
                        string group = plan.ConsumerGroup;
                        using var activity = plan.StartTraceVerbose("consumer.read-batch",
                                                                    tagsAction: t => PrepareTrace(t)
                                                                        .Add("consumer-group", group));

                        IConnectionMultiplexer conn = await _connFactory.GetAsync(cancellationToken);
                        IDatabaseAsync db = conn.GetDatabase();
                        try
                        {
                            values = await db.StreamReadGroupAsync(
                                                                fullUri,
                                                                group,
                                                                plan.ConsumerName,
                                                                position: StreamPosition.NewMessages,
                                                                count: bachSize,
                                                                flags: flags)
                                            .WithCancellation(ct, () => Array.Empty<StreamEntry>())
                                            .WithCancellation(cancellationToken, () => Array.Empty<StreamEntry>());
                            PrepareMeter(ConcumeBatchCountCounter).Add(1);
                            activity?.SetTag("count", values.Length);
                            PrepareMeter(ConcumeEventsCounter).Add(values.Length);
                        }
                        #region Exception Handling

                        catch (RedisServerException ex) when (ex.Message.StartsWith("NOGROUP"))
                        {
                            PrepareMeter(ConcumeBatchFailureCounter).Add(1);
                            logger.LogWarning(ex, ex.Message);
                            await _connFactory.CreateConsumerGroupIfNotExistsAsync(
                                    plan,
                                    plan.ConsumerGroup,
                                    logger, cancellationToken);
                        }
                        catch (RedisServerException ex)
                        {
                            PrepareMeter(ConcumeBatchFailureCounter).Add(1);
                            logger.LogWarning(ex, ex.Message);
                            await _connFactory.CreateConsumerGroupIfNotExistsAsync(
                                    plan,
                                    plan.ConsumerGroup,
                                    logger, cancellationToken);
                        }

                        #endregion // Exception Handling
                    }
                    StreamEntry[] results = values ?? Array.Empty<StreamEntry>();
                    return results;
                }, cancellationToken);
                return r;
            }
            #region Exception Handling

            catch (RedisTimeoutException ex)
            {
                logger.LogWarning(ex, "Event source [{source}] by [{consumer}]: Timeout", fullUri, plan.ConsumerName);
                return Array.Empty<StreamEntry>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fail to read from event source [{source}] by [{consumer}]", fullUri, plan.ConsumerName);
                return Array.Empty<StreamEntry>();
            }

            #endregion // Exception Handling
        }

        #endregion // ReadBatchAsync

        #region ReadSelfPending

        // Check for pending messages of the current consumer (crash scenario)
        async Task<StreamEntry[]> ReadSelfPending()
        {

            StreamEntry[] values = Array.Empty<StreamEntry>();
            if (!isFirstBatchOrFailure)
                return values;

            using var _ = plan.StartTraceVerbose("consumer.self-pending");

            IConnectionMultiplexer conn = await _connFactory.GetAsync(cancellationToken);
            IDatabaseAsync db = conn.GetDatabase();
            try
            {
                StreamPendingMessageInfo[] pendMsgInfo = await db.StreamPendingMessagesAsync(
                                            fullUri,
                                            plan.ConsumerGroup,
                                            options.BatchSize,
                                            plan.ConsumerName,
                                            flags: CommandFlags.DemandMaster);
                if (pendMsgInfo != null && pendMsgInfo.Length != 0)
                {
                    var ids = pendMsgInfo
                        .Select(m => m.MessageId).ToArray();
                    if (ids.Length != 0)
                    {
                        values = await db.StreamClaimAsync(fullUri,
                                                  plan.ConsumerGroup,
                                                  plan.ConsumerName,
                                                  0,
                                                  ids,
                                                  flags: CommandFlags.DemandMaster);
                        values = values ?? Array.Empty<StreamEntry>();
                        _logger.LogInformation("Claimed messages: {ids}", ids);
                    }
                }

                return values;
            }
            #region Exception Handling

            catch (RedisServerException ex) when (ex.Message.StartsWith("NOGROUP"))
            {
                await _connFactory.CreateConsumerGroupIfNotExistsAsync(
                        plan,
                        plan.ConsumerGroup,
                        logger, cancellationToken);
                return Array.Empty<StreamEntry>();
            }

            #endregion // Exception Handling
        }

        #endregion // ReadSelfPending

        #region ClaimStaleMessages

        // Taking work from other consumers which have log-time's pending messages
        async Task<StreamEntry[]> ClaimStaleMessages(
            int emptyBatchCount,
            StreamEntry[] values,
            CancellationToken ct)
        {
            var logger = plan.Logger ?? _logger;
            ct.ThrowIfCancellationRequested();
            if (values.Length != 0) return values;
            if (emptyBatchCount < claimingTrigger.EmptyBatchCount)
                return values;
            using var _ = plan.StartTraceVerbose("consumer.stale-events");
            try
            {
                IDatabaseAsync db = await _connFactory.GetDatabaseAsync(ct);
                StreamPendingInfo pendingInfo;
                using (plan.StartTraceVerbose("consumer.events-stealing.pending"))
                {
                    pendingInfo = await db.StreamPendingAsync(fullUri, plan.ConsumerGroup, flags: CommandFlags.DemandMaster);
                }
                PrepareMeter(StealCountCounter).Add(1);
                foreach (var c in pendingInfo.Consumers)
                {
                    var self = c.Name == plan.ConsumerName;
                    if (self) continue;
                    try
                    {
                        StreamPendingMessageInfo[] pendMsgInfo;
                        using (plan.StartTraceVerbose("consumer.events-stealing.pending-events",
                                                    tagsAction: t => PrepareTrace(t).Add("from-consumer", c.Name)))
                        {
                            pendMsgInfo = await db.StreamPendingMessagesAsync(
                            fullUri,
                            plan.ConsumerGroup,
                            10,
                            c.Name,
                            pendingInfo.LowestPendingMessageId,
                            pendingInfo.HighestPendingMessageId,
                            flags: CommandFlags.DemandMaster);
                        }

                        RedisValue[] ids = pendMsgInfo
                                    .Where(x => x.IdleTimeInMilliseconds > minIdleTime)
                                    .Select(m => m.MessageId).ToArray();
                        if (ids.Length == 0)
                            continue;

                        #region Log
                        logger.LogInformation("Event Source Consumer [{name}]: Claimed {count} events, from Consumer [{name}]", plan.ConsumerName, c.PendingMessageCount, c.Name);

                        #endregion // Log

                        int count = ids.Length;
                        PrepareMeter(StealAmountCounter).WithTag("from-consumer", c.Name)
                                .Add(count);
                        // will claim events only if older than _setting.ClaimingTrigger.MinIdleTime
                        using (plan.StartTraceVerbose("consumer.events-stealing.claim",
                                                    tagsAction: t => PrepareTrace(t)
                                                                    .Add("from-consumer", c.Name)
                                                                    .Add("message-count", count)))
                        {
                            values = await db.StreamClaimAsync(fullUri,
                                                  plan.ConsumerGroup,
                                                  c.Name,
                                                  minIdleTime,
                                                  ids,
                                                  flags: CommandFlags.DemandMaster);
                        }
                        if (values.Length != 0)
                            logger.LogInformation("Event Source Consumer [{name}]: Claimed {count} messages, from Consumer [{name}]", plan.ConsumerName, c.PendingMessageCount, c.Name);
                    }
                    #region Exception Handling

                    catch (RedisTimeoutException ex)
                    {
                        logger.LogWarning(ex, "Timeout (handle pending): {name}{self}", c.Name, self);
                        continue;
                    }

                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Fail to claim pending: {name}{self}", c.Name, self);
                    }

                    #endregion // Exception Handling

                    if (values != null && values.Length != 0)
                        return values;
                }
            }
            #region Exception Handling

            catch (RedisConnectionException ex)
            {
                _logger.LogWarning(ex, "Fail to claim REDIS's pending");
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Fail to claim pending");
            }

            #endregion // Exception Handling

            return Array.Empty<StreamEntry>();
        }

        #endregion // ClaimStaleMessages

        #region AckAsync

        // Acknowledge event handling (prevent re-consuming of the message).
        async ValueTask AckAsync(RedisValue messageId)
        {
            try
            {
                IConnectionMultiplexer conn = await _connFactory.GetAsync(cancellationToken);
                IDatabaseAsync db = conn.GetDatabase();
                // release the event (won't handle again in the future)
                await db.StreamAcknowledgeAsync(fullUri,
                                                plan.ConsumerGroup,
                                                messageId,
                                                flags: CommandFlags.DemandMaster);
            }
            catch (Exception ex)
            {
                // TODO: [bnaya 2020-10] do better handling (re-throw / swallow + reason) currently logged at the wrapping class
                logger.LogWarning(ex.FormatLazy(), $"Fail to acknowledge message [{messageId}]");
                throw;
            }
        }

        #endregion // AckAsync

        #region ReleaseAsync


        // Releases the messages (work around).
        async Task ReleaseAsync(RedisValue[] freeTargets)
        {
            IConnectionMultiplexer conn = await _connFactory.GetAsync(cancellationToken);
            IDatabaseAsync db = conn.GetDatabase();
            try
            {
                using (plan.StartTraceVerbose("consumer.release-ownership",
                                                           tagsAction: t => PrepareTrace(t).Add("consumer-group", plan.ConsumerGroup)))
                {
                    await db.StreamClaimAsync(fullUri,
                                          plan.ConsumerGroup,
                                          RedisChannelConstants.NONE_CONSUMER,
                                          1,
                                          freeTargets,
                                          flags: CommandFlags.DemandMaster);
                }
                using (plan.StartTraceVerbose("consumer.release.delay",
                                              tagsAction: t => PrepareTrace(t).Add("delay", releaseDelay)))
                {
                    // let other potential consumer the chance of getting ownership
                    await Task.Delay(releaseDelay, cancellationToken);
                }
                if (releaseDelay < MAX_RELEASE_DELAY)
                    releaseDelay = Math.Min(releaseDelay * 2, MAX_RELEASE_DELAY);

                if (bachSize == options.BatchSize)
                    bachSize = 1;
                else
                    bachSize = Math.Min(bachSize * 2, options.BatchSize);
            }
            #region Exception Handling

            catch (RedisServerException ex) when (ex.Message.StartsWith("NOGROUP"))
            {
                await _connFactory.CreateConsumerGroupIfNotExistsAsync(
                        plan,
                        plan.ConsumerGroup,
                        logger, cancellationToken);
            }

            #endregion // Exception Handling  
        }

        #endregion // ReleaseAsync

        ITagAddition PrepareTrace(ITagAddition t) => t.Add("uri", uri).Add("env", env)
                                                                    .Add("group-name", consumerGroup);
        ICounterBuilder<int> PrepareMeter(Counter<int> t) => t.WithTag("uri", uri)
                                                                .WithTag("env", env)
                                                                .WithTag("group-name", consumerGroup);
    }

    #endregion // SubsribeToSingleAsync

    #region GetByIdAsync

    /// <summary>
    /// Gets announcement data by id.
    /// </summary>
    /// <param name="entryId">The entry identifier.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    async ValueTask<Announcement> IConsumerChannelProvider.GetByIdAsync(
        EventKey entryId,
        IConsumerPlan plan,
        CancellationToken cancellationToken)
    {
        string mtdName = $"{nameof(IConsumerChannelProvider)}.{nameof(IConsumerChannelProvider.GetByIdAsync)}";

        try
        {
            IConnectionMultiplexer conn = await _connFactory.GetAsync(cancellationToken);
            IDatabaseAsync db = conn.GetDatabase();
            StreamEntry entry = await FindAsync(entryId);

            #region var announcement = new Announcement(...)

            var meta = ExtractMetadata(entry, out var channelMeta);

            (Bucket segmets, Bucket interceptions) = await GetStorageAsync(plan, channelMeta, meta);

            var announcement = new Announcement
            {
                Metadata = meta,
                Segments = segmets,
                InterceptorsData = interceptions
            };

            #endregion // var announcement = new Announcement(...)

            return announcement;

            #region FindAsync

            async Task<StreamEntry> FindAsync(EventKey entryId)
            {
                string lookForId = (string)entryId;
                string fullUri = plan.FullUri();

                string originId = lookForId;
                int len = originId.IndexOf('-');
                string fromPrefix = originId.Substring(0, len);
                long start = long.Parse(fromPrefix);
                string startPosition = (start - 1).ToString();
                int iteration = 0;
                for (int i = 0; i < READ_BY_ID_ITERATIONS; i++) // up to 1000 items
                {
                    iteration++;
                    StreamEntry[] entries = await db.StreamReadAsync(
                                                            fullUri,
                                                            startPosition,
                                                            READ_BY_ID_CHUNK_SIZE,
                                                            CommandFlags.DemandMaster);
                    if (entries.Length == 0)
                        throw new KeyNotFoundException($"{mtdName} of [{lookForId}] from [{fullUri}] return nothing, start at ({startPosition}, iteration = {iteration}).");
                    string k = string.Empty;
                    foreach (StreamEntry e in entries)
                    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        k = e.Id;
                        string ePrefix = k.Substring(0, len);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                        long comp = long.Parse(ePrefix);
                        if (comp < start)
                            continue; // not there yet
                        if (k == lookForId)
                        {
                            return e;
                        }
                        if (ePrefix != fromPrefix)
                            throw new KeyNotFoundException($"{mtdName} of [{lookForId}] from [{fullUri}] return not exists.");
                    }
                    startPosition = k; // next batch will start from last entry
                }
                throw new KeyNotFoundException($"{mtdName} of [{lookForId}] from [{fullUri}] return not found.");
            }

            #endregion // FindAsync
        }
        #region Exception Handling

        catch (Exception ex)
        {
            string key = plan.FullUri();
            _logger.LogError(ex.FormatLazy(), "{method} Failed: Entry [{entryId}] from [{key}] event stream",
                mtdName, entryId, key);
            throw;
        }

        #endregion // Exception Handling
    }

    #endregion // GetByIdAsync

    #region GetAsyncEnumerable

    /// <summary>
    /// Gets asynchronous enumerable of announcements.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    async IAsyncEnumerable<Announcement> IConsumerChannelProvider.GetAsyncEnumerable(
                IConsumerPlan plan,
                ConsumerAsyncEnumerableOptions? options,
                [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IConnectionMultiplexer conn = await _connFactory.GetAsync(cancellationToken);
        IDatabaseAsync db = conn.GetDatabase();
        var loop = AsyncLoop().WithCancellation(cancellationToken);
        await foreach (StreamEntry entry in loop)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            #region var announcement = new Announcement(...)

            var meta = ExtractMetadata(entry, out var channelMeta);
            var filter = options?.OperationFilter;
            if (filter != null && !filter(meta))
                continue;

            (Bucket segmets, Bucket interceptions) = await GetStorageAsync(plan, channelMeta, meta);

            var announcement = new Announcement
            {
                Metadata = meta,
                Segments = segmets,
                InterceptorsData = interceptions
            };

            #endregion // var announcement = new Announcement(...)

            yield return announcement;
        }

        #region AsyncLoop

        async IAsyncEnumerable<StreamEntry> AsyncLoop()
        {
            string fullUri = plan.FullUri();

            int iteration = 0;
            RedisValue startPosition = options?.From ?? BEGIN_OF_STREAM;
            TimeSpan delay = TimeSpan.Zero;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested) yield break;

                iteration++;
                StreamEntry[] entries = await db.StreamReadAsync(
                                                        fullUri,
                                                        startPosition,
                                                        READ_BY_ID_CHUNK_SIZE,
                                                        CommandFlags.DemandMaster);
                if (entries.Length == 0)
                {
                    if (options?.ExitWhenEmpty ?? true) yield break;
                    delay = await DelayIfEmpty(plan, delay, cancellationToken);
                    continue;
                }
                string k = string.Empty;
                foreach (StreamEntry e in entries)
                {
                    if (cancellationToken.IsCancellationRequested) yield break;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    k = e.Id;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                    if (options?.To != null && string.Compare(options?.To, k) < 0)
                        yield break;
                    yield return e;
                }
                startPosition = k; // next batch will start from last entry
            }
        }

        #endregion // AsyncLoop
    }

    #endregion // GetAsyncEnumerable

    #region GetStorageAsync

    /// <summary>
    /// Gets the storage information.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="channelMeta">The channel meta.</param>
    /// <param name="meta">The meta.</param>
    /// <returns></returns>
    private async ValueTask<(Bucket segments, Bucket interceptions)> GetStorageAsync(
                                        IConsumerPlan plan,
                                        Dictionary<RedisValue, RedisValue> channelMeta,
                                        Metadata meta)
    {
        ValueTask<Bucket> segmetsTask = (meta.StorageTypes & EventBucketCategories.Segments) switch
        {
            EventBucketCategories.None => Bucket.Empty.ToValueTask(),
            _ => GetBucketAsync(plan, channelMeta, meta, EventBucketCategories.Segments)
        };
        ValueTask<Bucket> interceptionsTask = (meta.StorageTypes & EventBucketCategories.Interceptions) switch
        {
            EventBucketCategories.None => Bucket.Empty.ToValueTask(),
            _ => GetBucketAsync(plan, channelMeta, meta, EventBucketCategories.Interceptions)
        };
        Bucket segmets = await segmetsTask;
        Bucket interceptions = await interceptionsTask;
        return (segmets, interceptions);
    }

    #endregion // GetStorageAsync

    #region Task<Bucket> GetBucketAsync(StorageType storageType) // local function

    /// <summary>
    /// Gets a data bucket.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="channelMeta">The channel meta.</param>
    /// <param name="meta">The meta.</param>
    /// <param name="storageType">Type of the storage.</param>
    /// <returns></returns>
    private async ValueTask<Bucket> GetBucketAsync(
                                        IConsumerPlan plan,
                                        Dictionary<RedisValue, RedisValue> channelMeta,
                                        Metadata meta,
                                        EventBucketCategories storageType)
    {

        IEnumerable<IConsumerStorageStrategyWithFilter> strategies = plan.StorageStrategies;
        strategies = strategies.Where(m => m.IsOfCategory(storageType));
        Bucket bucket = Bucket.Empty;
        if (strategies.Any())
        {
            foreach (var strategy in strategies)
            {
                using (plan.StartTraceDebug($"consumer.{strategy.Name}-storage.{storageType}.get"))
                {
                    bucket = await strategy.LoadBucketAsync(meta, bucket, storageType, LocalGetProperty);
                }
            }
        }
        else
        {
            using (plan.StartTraceDebug($"consumer.{_defaultStorageStrategy.Name}-storage.{storageType}.get"))
            {
                bucket = await _defaultStorageStrategy.LoadBucketAsync(meta, bucket, storageType, LocalGetProperty);
            }
        }

        return bucket;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
        string LocalGetProperty(string k) => (string)channelMeta[k];
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
    }

    #endregion // Task<Bucket> GetBucketAsync(StorageType storageType) // local function

    #region ExtractMetadata

    /// <summary>
    /// Extracts the metadata.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="channelMeta">The channel meta.</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentException">EventKey</exception>
    /// <exception cref="EventSourcing.Backbone.EventSourcingException">Metadata</exception>
    private static Metadata ExtractMetadata(StreamEntry result, out Dictionary<RedisValue, RedisValue> channelMeta)
    {
        Metadata meta;
        channelMeta = result.Values.ToDictionary(m => m.Name, m => m.Value);
        string? metaJson = channelMeta?[META_SLOT];
        if (metaJson == null)
            throw new EventSourcingException("Couldn't find the metadata slot");
        string eventKey = ((string?)result.Id) ?? throw new ArgumentException(nameof(MetadataExtensions.Empty.EventKey));
        meta = JsonSerializer.Deserialize<Metadata>(metaJson, EventSourceOptions.SerializerOptions) ?? throw new EventSourcingException(nameof(Metadata));

        long? producedAtUnix = (long?)channelMeta?[nameof(MetadataExtensions.Empty.ProducedAt)];
        if (producedAtUnix == null)
        {
            meta = meta with { EventKey = eventKey };
        }
        else
        {
            DateTimeOffset producedAt = DateTimeOffset.FromUnixTimeSeconds((long)producedAtUnix);
            meta = meta with { EventKey = eventKey, ProducedAt = producedAt };
        }
        return meta;
    }

    #endregion // ExtractMetadata

    #region DelayIfEmpty

    // avoiding system hit when empty (mitigation of self DDoS)
    /// <summary>
    /// Delays if empty.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="previousDelay">The previous delay.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    private async Task<TimeSpan> DelayIfEmpty(IPlanBase plan, TimeSpan previousDelay, CancellationToken cancellationToken)
    {
        var cfg = _setting.DelayWhenEmptyBehavior;
        var newDelay = cfg.CalcNextDelay(previousDelay, cfg);
        var limitDelay = Min(cfg.MaxDelay.TotalMilliseconds, newDelay.TotalMilliseconds);
        newDelay = TimeSpan.FromMilliseconds(limitDelay);
        using (plan.StartTraceDebug("consumer.delay.when-empty-queue",
                                        tagsAction: t => t.Add("delay", newDelay)))
        {
            await Task.Delay(newDelay, cancellationToken);
        }
        return newDelay;
    }

    #endregion // DelayIfEmpty

    #region GetKeysUnsafeAsync

    /// <summary>
    /// Gets the keys unsafe asynchronous.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public async IAsyncEnumerable<string> GetKeysUnsafeAsync(
                    string pattern,
                    [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IConnectionMultiplexer multiplexer = await _connFactory.GetAsync(cancellationToken);
        var distict = new HashSet<string>();
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (EndPoint endpoint in multiplexer.GetEndPoints())
            {
                IServer server = multiplexer.GetServer(endpoint);
                // TODO: [bnaya 2020_09] check the pagination behavior
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8604 // Possible null reference argument.
                await foreach (string key in server.KeysAsync(pattern: pattern))
                {
                    if (distict.Contains(key))
                        continue;
                    distict.Add(key);
                    yield return key;
                }
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            }
        }
    }

    #endregion // GetKeysUnsafeAsync
}
