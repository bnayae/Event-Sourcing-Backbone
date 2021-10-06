﻿namespace Weknow.EventSource.Backbone.Building
{
    /// <summary>
    /// Enable configuration.
    /// </summary>
    public interface IProducerOptionsBuilder
        : IProducerEnvironmentBuilder
    {
        /// <summary>
        /// Apply configuration.
        /// </summary>
        IProducerEnvironmentBuilder WithOptions(EventSourceOptions options);
    }
}
