using static System.Math;


namespace EventSourcing.Backbone.Channels.RedisProvider
{
    /// <summary>
    /// Behavior of delay when empty
    /// </summary>
    public record DelayWhenEmptyBehavior
    {
        private const int MIN_DELAY_MILLI = 2;
        public static readonly DelayWhenEmptyBehavior Default = new DelayWhenEmptyBehavior();

        /// <summary>
        /// Gets or sets the maximum delay.
        /// </summary>
        public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The increment factor when increasing the delay (hang on empty).
        /// The previous delay will multiply by this factor + Ceiling to endure increment.
        /// </summary>
        public double DelayFactor { get; init; } = 1.2;


        /// <summary>
        /// Gets or sets the next delay.
        /// </summary>
        public Func<TimeSpan, DelayWhenEmptyBehavior, TimeSpan> CalcNextDelay { get; init; } = DefaultCalcNextDelay;

        /// <summary>
        /// Default calculation of next delay.
        /// </summary>
        /// <param name="previous">The previous delay.</param>
        /// <param name="setting">The setting.</param>
        /// <returns></returns>
        private static TimeSpan DefaultCalcNextDelay(TimeSpan previous, DelayWhenEmptyBehavior setting)
        {
            var prevMilli = previous.TotalMilliseconds;
            var milli = Max(Math.Ceiling(prevMilli * setting.DelayFactor), MIN_DELAY_MILLI);
            return TimeSpan.FromMilliseconds(milli);
        }
    }
}