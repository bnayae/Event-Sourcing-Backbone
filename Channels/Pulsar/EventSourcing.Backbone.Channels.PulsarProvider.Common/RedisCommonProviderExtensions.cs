using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using static EventSourcing.Backbone.Private.EventSourceTelemetry;

namespace EventSourcing.Backbone.Private
{
    /// <summary>
    /// Redis common provider extensions
    /// </summary>
    public static class RedisCommonProviderExtensions
    {
        private const int DELAY_ON_MISSING_KEY = 5;
        private const int MIN_DELAY = 2;
        private const int SPIN_LIMIT = 30;
        private const int MAX_DELAY = 3_000;
        private static Counter<int> KeyMissingCounter = EMeter.CreateCounter<int>("evt-src.sys.key-missing", "count", "count missing key events");
        private static Counter<int> CreateConsumerGroupCounter = EMeter.CreateCounter<int>("evt-src.sys.create-consumer-group", "count", "creating a consumer group");
        private static Counter<int> CreateConsumerGroupRetryCounter = EMeter.CreateCounter<int>("evt-src.sys.create-consumer-group-retry", "count", "retries of creating a consumer group");

        private static readonly AsyncLock _lock = new AsyncLock(TimeSpan.FromSeconds(20));

        /// <summary>
        /// Creates the consumer group if not exists asynchronous.
        /// </summary>
        /// <param name="connFactory">The connection factory.</param>
        /// <param name="plan">The plan.</param>
        /// <param name="consumerGroup">The consumer group.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static async Task CreateConsumerGroupIfNotExistsAsync(
                        this IEventSourceRedisConnectionFactory connFactory,
                        IConsumerPlan plan,
                        string consumerGroup,
                        ILogger logger,
                        CancellationToken cancellationToken)
        {
            Env env = plan.Environment;
            string uri = plan.UriDash;
            string fullUri = plan.FullUri();

            StreamGroupInfo[] groupsInfo = Array.Empty<StreamGroupInfo>();
            using var track = plan.StartTraceVerbose("consumer.create-consumer-group-process",
                                        tagsAction: t => PrepareTrace(t));

            PrepareMeter(CreateConsumerGroupCounter).Add(1);
            int delay = MIN_DELAY;
            bool exists = false;
            int tryNumber = 0;
            var retryCounter = PrepareMeter(CreateConsumerGroupRetryCounter);
            var missingCounter = PrepareMeter(KeyMissingCounter);
            while (groupsInfo.Length == 0)
            {
                if (tryNumber != 0)
                    retryCounter.Add(1);
                tryNumber++;

                IConnectionMultiplexer conn = await connFactory.GetAsync(cancellationToken);
                IDatabaseAsync db = conn.GetDatabase();
                try
                {
                    #region delay on retry

                    if (tryNumber > SPIN_LIMIT)
                    {
                        delay = Math.Min(delay * 2, MAX_DELAY);
                        using (plan.StartTraceDebug("consumer.delay.key-not-exists",
                                            tagsAction: t => PrepareTrace(t)
                                                            .Add("delay", delay)
                                                            .Add("try-number", tryNumber)))
                        {
                            await Task.Delay(delay);
                        }
                        if (tryNumber % 10 == 0)
                        {
                            logger.LogWarning("Create Consumer Group If Not Exists: still waiting {info}", CurrentInfo());
                        }
                    }

                    #endregion // delay on retry

                    #region Validation (if key exists)

                    if (!await db.KeyExistsAsync(fullUri,
                                                 flags: CommandFlags.DemandMaster))
                    {
                        missingCounter.Add(1);
                        await Task.Delay(DELAY_ON_MISSING_KEY);
                        if (tryNumber == 0 || tryNumber > SPIN_LIMIT)
                            logger.LogDebug("Key not exists (yet): {info}", CurrentInfo());
                        continue;
                    }

                    #endregion // Validation (if key exists)

                    using (plan.StartTraceVerbose("consumer.get-consumer-group-info",
                                        tagsAction: t => PrepareTrace(t)
                                                        .Add("try-number", tryNumber)))
                    {
                        using var lk = await _lock.AcquireAsync(cancellationToken);
                        groupsInfo = await db.StreamGroupInfoAsync(
                                                    fullUri,
                                                    flags: CommandFlags.DemandMaster);
                        exists = groupsInfo.Any(m => m.Name == consumerGroup);
                    }
                }
                #region Exception Handling

                catch (RedisServerException ex)
                {
                    if (await db.KeyExistsAsync(fullUri,
                                                 flags: CommandFlags.DemandMaster))
                    {
                        logger.LogWarning(ex, "Create Consumer Group If Not Exists: failed. {info}", CurrentInfo());
                    }
                    else
                    {
                        logger.LogDebug(ex, "Create Consumer Group If Not Exists: failed. {info}", CurrentInfo());
                    }
                }
                catch (RedisConnectionException ex)
                {
                    logger.LogWarning(ex.FormatLazy(), "Create Consumer Group If Not Exists: connection failure. {info}", CurrentInfo());
                }
                catch (RedisTimeoutException ex)
                {
                    logger.LogWarning(ex.FormatLazy(), "Create Consumer Group If Not Exists:  timeout failure. {info}", CurrentInfo());
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex.FormatLazy(), "Create Consumer Group If Not Exists:  unexpected failure. {info}", CurrentInfo());
                }

                #endregion // Exception Handling
                if (!exists)
                {
                    try
                    {
                        using (plan.StartTraceDebug("consumer.create-consumer-group",
                                            tagsAction: t => PrepareTrace(t)
                                                            .Add("try-number", tryNumber)))
                        {
                            using var lk = await _lock.AcquireAsync(cancellationToken);
                            if (await db.StreamCreateConsumerGroupAsync(fullUri,
                                                                    consumerGroup,
                                                                    StreamPosition.Beginning,
                                                                    flags: CommandFlags.DemandMaster))
                            {
                                break;
                            }
                        }
                    }
                    #region Exception Handling

                    catch (RedisServerException ex)
                    {
                        logger.LogWarning(ex.FormatLazy(), $"""
                                                {nameof(CreateConsumerGroupIfNotExistsAsync)}.StreamCreateConsumerGroupAsync: 
                                                failed & still waiting 
                                                {CurrentInfo()}
                                                """);
                    }
                    catch (RedisConnectionException ex)
                    {
                        logger.LogWarning(ex.FormatLazy(), $"""
                                                {nameof(CreateConsumerGroupIfNotExistsAsync)}.StreamCreateConsumerGroupAsync: 
                                                Connection failure 
                                                {CurrentInfo()}
                                                """);
                    }
                    catch (ObjectDisposedException)
                    {
                        logger.LogWarning($"""
                                                {nameof(CreateConsumerGroupIfNotExistsAsync)}.StreamCreateConsumerGroupAsync: 
                                                Connection might not being available 
                                                {CurrentInfo()}
                                                """);
                    }

                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, $"""
                                                {nameof(CreateConsumerGroupIfNotExistsAsync)}.StreamCreateConsumerGroupAsync: 
                                                unexpected failure 
                                                {CurrentInfo()}
                                                """);
                    }

                    #endregion // Exception Handling
                }

                #region string CurrentInfo()

                string CurrentInfo() => @$"
Try number:     {tryNumber}
Stream key:     {uri}
Consumer Group: {consumerGroup}
Is Connected:   {db.Multiplexer.IsConnected}
Configuration:  {db.Multiplexer.Configuration}
";

                #endregion // string CurrentInfo()
            }


            ITagAddition PrepareTrace(ITagAddition t) => t.Add("uri", uri)
                                                            .Add("env", env)
                                                            .Add("group-name", consumerGroup);
            ICounterBuilder<int> PrepareMeter(Counter<int> t) => t.WithTag("uri", uri)
                                                                    .WithTag("env", env)
                                                                    .WithTag("group-name", consumerGroup);
        }
    }
}
