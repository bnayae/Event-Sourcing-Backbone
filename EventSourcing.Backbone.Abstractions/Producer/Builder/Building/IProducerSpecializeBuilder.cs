namespace EventSourcing.Backbone.Building
{
    /// <summary>
    /// Event Source producer builder.
    /// </summary>
    public interface IProducerSpecializeBuilder : IProducerBuilderEnvironment<IProducerSpecializeBuilder>, IProducerRawBuilder
    {

        /// <summary>
        /// <![CDATA[ Ceate Producer proxy for specific events sequence.
        /// Event sequence define by an interface which declare the
        /// operations which in time will be serialized into event's
        /// messages.
        /// This interface can be use as a proxy in the producer side,
        /// and as adapter on the consumer side.
        /// All method of the interface should represent one-way communication pattern
        /// and return Task or ValueTask (not Task<T> or ValueTask<T>).
        /// Nothing but method allowed on this interface]]>
        /// </summary>
        /// <typeparam name="T">The contract of the proxy / adapter</typeparam>
        /// <param name="factory">The factory.</param>
        /// <returns></returns>
        T Build<T>(Func<IProducerPlan, T> factory)
            where T : class;

        /// <summary>
        /// Enable dynamic transformation of the stream id before sending.
        /// Can use for scenario like routing between environment like dev vs. prod or aws vs azure.
        /// </summary>
        IProducerOverrideBuilder<T> Specialize<T>()
            where T : class;
    }
}
