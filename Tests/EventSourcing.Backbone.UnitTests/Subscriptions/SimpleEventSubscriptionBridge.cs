﻿using EventSourcing.Backbone.UnitTests.Entities;
using EventSourcing.Backbone.UnitTests.Entities.Generated;

namespace EventSourcing.Backbone
{
    using static SimpleEventSignatures.ACTIVE;

    /// <summary>
    /// In-Memory Channel (excellent for testing)
    /// </summary>
    /// <seealso cref="EventSourcing.Backbone.IProducerChannelProvider" />
    public class SimpleEventSubscriptionBridge : ISubscriptionBridge
    {
        private readonly ISimpleEventConsumer _target;

        #region Ctor

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="target">The target.</param>
        public SimpleEventSubscriptionBridge(ISimpleEventConsumer target)
        {
            _target = target;
        }

        #endregion // Ctor

        async Task<bool> ISubscriptionBridge.BridgeAsync(Announcement announcement, IConsumerBridge consumerBridge, IPlanBase plan)
        {
            var meta = ConsumerContext.Context;
            switch (announcement.Metadata.Signature.ToString())
            {
                case ExecuteAsync.V0.P_String_Int32.SignatureString:
                    {
                        var p0 = await consumerBridge.GetParameterAsync<string>(announcement, "key");
                        var p1 = await consumerBridge.GetParameterAsync<int>(announcement, "value");
                        await _target.ExecuteAsync(meta, p0, p1);
                        return true;
                    }
                case RunAsync.V0.P_Int32_DateTime.SignatureString:
                    {
                        var p0 = await consumerBridge.GetParameterAsync<int>(announcement, "id");
                        var p1 = await consumerBridge.GetParameterAsync<DateTime>(announcement, "date");
                        await _target.RunAsync(meta, p0, p1);
                        return true;
                    }
                default:
                    break;
            }
            return false;
        }
    }
}
