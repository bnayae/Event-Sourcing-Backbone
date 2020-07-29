﻿using System;
using System.Collections;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Weknow.EventSource.Backbone.Building;

namespace Weknow.EventSource.Backbone
{

    public class ConsumerTestChannel : IConsumerChannelProvider
    {
        private readonly Channel<Announcement> _channel;

        #region Ctor

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="channel">The channel.</param>
        public ConsumerTestChannel(Channel<Announcement> channel)
        {
            _channel = channel;
        }

        #endregion // Ctor

        #region ReceiveAsync

        /// <summary>
        /// Receives the asynchronous.
        /// </summary>
        /// <param name="func">The function.</param>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async ValueTask ReceiveAsync(
                    Func<Announcement, ValueTask> func,
                    IEventSourceConsumerOptions options,
                    CancellationToken cancellationToken)
        {
            while (!_channel.Reader.Completion.IsCompleted &&
                   !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var announcement = await _channel.Reader.ReadAsync(cancellationToken);
                    await func(announcement);
                }
                catch (ChannelClosedException) { }
            }
        }


        #endregion // ReceiveAsync
    }
}
