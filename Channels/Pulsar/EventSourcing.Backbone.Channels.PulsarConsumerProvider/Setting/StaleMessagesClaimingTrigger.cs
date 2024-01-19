// TODO: [bnaya 2021-02] use Record

namespace EventSourcing.Backbone.Channels.RedisProvider
{
    /// <summary>
    /// Define when to claim stale (long waiting) messages from other consumers
    /// </summary>
    public class StaleMessagesClaimingTrigger
    {
        public static readonly StaleMessagesClaimingTrigger Default = new StaleMessagesClaimingTrigger();

        /// <summary>
        /// Empty batch count define number of empty fetching cycle in a row 
        /// which will trigger operation of trying to get stale messages from other consumers.
        /// </summary>
        public ushort EmptyBatchCount { get; set; } = 1000;

        /// <summary>
        /// The minimum message idle time to allow the reassignment of the message(s).
        /// </summary>
        public TimeSpan MinIdleTime { get; set; } = TimeSpan.FromMinutes(20);

        ///// <summary>
        ///// Define schedule which will trigger 
        ///// operation of trying to get stale messages from other consumers.
        ///// </summary>
        //public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(10);
    }
}