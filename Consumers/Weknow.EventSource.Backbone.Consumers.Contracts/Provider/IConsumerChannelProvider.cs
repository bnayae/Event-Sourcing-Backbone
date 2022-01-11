﻿using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Weknow.EventSource.Backbone
{
    /// <summary>
    /// Channel provider responsible for passing the actual message 
    /// from producer to consumer. 
    /// </summary>
    public interface IConsumerChannelProvider
    {
        /// <summary>
        /// Subscribe to the channel for specific metadata.
        /// </summary>
        /// <param name="plan">The consumer plan.</param>
        /// <param name="func">The function.</param>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// When completed
        /// </returns>
        ValueTask SubsribeAsync(
                    IConsumerPlan plan,
                    Func<Announcement, IAck, ValueTask> func,
                    ConsumerOptions options,
                    CancellationToken cancellationToken);

        /// <summary>
        /// Gets the by identifier asynchronous.
        /// </summary>
        /// <param name="entryId">The entry identifier.</param>
        /// <param name="plan">The plan.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        ValueTask<Announcement> GetByIdAsync(
                    EventKey entryId,
                    IConsumerPlan plan,
                    CancellationToken cancellationToken);


        //IAsyncEnumerable<Announcement> GetAsyncEnumerable(
        //            IConsumerPlan plan,
        //            string from,
        //            string to,
        //            string? operationFilter, // TODO: predicate
        //            CancellationToken cancellationToken);
    }
}