using Polly;


namespace EventSourcing.Backbone.Channels.RedisProvider
{
    /// <summary>
    /// Represent specific setting of the consumer channel
    /// </summary>
    public record RedisConsumerChannelSetting
    {
        public static readonly RedisConsumerChannelSetting Default = new RedisConsumerChannelSetting();


        /// <summary>
        /// Gets or sets the resilience policy.
        /// </summary>
        public ResiliencePolicies Policy { get; init; } = new ResiliencePolicies();

        /// <summary>
        /// Behavior of delay when empty
        /// </summary>
        public DelayWhenEmptyBehavior DelayWhenEmptyBehavior { get; init; } = DelayWhenEmptyBehavior.Default;

        #region Cast overloads

        /// <summary>
        /// Performs an implicit conversion.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator RedisConsumerChannelSetting(AsyncPolicy? policy)
        {
            if (policy == null) return Default;

            return Default with { Policy = policy };
        }

        #endregion // Cast overloads
    }
}