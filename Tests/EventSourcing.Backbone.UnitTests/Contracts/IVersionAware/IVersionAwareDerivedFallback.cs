﻿#pragma warning disable S1133 // Deprecated code should be removed
using Microsoft.Extensions.Logging;

namespace EventSourcing.Backbone.UnitTests;

using Generated.VersionAwareDerivedFallback;

/// <summary>
/// Test contract
/// </summary>
[EventsContract(EventsContractType.Producer, MinVersion = 1, VersionNaming = VersionNaming.Append)]
[EventsContract(EventsContractType.Consumer, MinVersion = 2, VersionNaming = VersionNaming.AppendUnderscore)]
[Obsolete("This interface is base for code generation, please use ISimpleEventProducer or ISimpleEventConsumer")]
public interface IVersionAwareDerivedFallback: IVersionAwareBase
{
    #region Fallback

    /// <summary>
    /// Consumers the fallback.
    /// Excellent for Migration scenario
    /// </summary>
    /// <param name="ctx">The context.</param>
    /// <param name="target">The target.</param>
    /// <returns></returns>
    public static async Task<bool> Fallback(IConsumerInterceptionContext ctx, IVersionAwareDerivedFallbackConsumer target)
    {
        ILogger logger = ctx.Logger;
        ConsumerContext consumerContext = ctx.Context;
        Metadata meta = consumerContext.Metadata;

        var (succeed1, data1) = await ctx.TryGetExecuteAsync_V0_String_Int32_DeprecatedAsync();
        if (succeed1)
        {
            await target.Execute_3Async(consumerContext, $"{data1!.value}_{data1.key}");
            await ctx.AckAsync();
            return true;
        }
        var (succeed2, data2) = await ctx.TryGetExecuteAsync_V1_Int32_DeprecatedAsync();
        if (succeed2)
        {
            await target.Execute_3Async(consumerContext, data2!.value.ToString());
            await ctx.AckAsync();
            return true;
        }
        var (succeed3, data3) = await ctx.TryGetExecuteAsync_V4_TimeSpan_DeprecatedAsync();
        if (succeed3)
        {
            await target.Execute_3Async(consumerContext, data3!.value.ToString());
            await ctx.AckAsync();
            return true;
        }
        logger.LogWarning("Fallback didn't handle: {uri}, {signature}", meta.Uri, meta.Signature);
        return false;
    }

    #endregion // Fallback
}
