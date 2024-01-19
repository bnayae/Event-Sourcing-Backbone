using Polly;
using Polly.CircuitBreaker;

namespace EventSourcing.Backbone.Channels.RedisProvider
{
    /// <summary>
    /// Define when to claim stale (long waiting) messages from other consumers
    /// </summary>
    public record ResiliencePolicies
    {
        public static readonly ResiliencePolicies Empty = new ResiliencePolicies();

        #region Ctor

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="policy">The policy.</param>
        public ResiliencePolicies(AsyncPolicy policy)
        {
            Policy = policy;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="onBreak">The on break.</param>
        /// <param name="onReset">The on reset.</param>
        /// <param name="onHalfOpen">The on half open.</param>
        /// <param name="onRetry">The on retry.</param>
        public ResiliencePolicies(
            Action<Exception, CircuitState, TimeSpan, Context>? onBreak = null,
            Action<Context>? onReset = null,
            Action? onHalfOpen = null,
            Func<Exception, TimeSpan, int, Context, Task>? onRetry = null
            )
        {
            var onBreak_ = (onBreak ?? ((ex, state, duration, ctx) => { }));
            var onReset_ = onReset ?? ((Action<Context>)((ctx) => { }));
            var onHalfOpen_ = onHalfOpen ?? (() => { });
            var onRetry_ = onRetry ?? ((ex, duration, retryCount, ctx) => Task.CompletedTask);

            //_onBreak = onBreak_;
            //_onReset = onReset_;
            //_onHalfOpen = onHalfOpen_;
            //_onRetry = onRetry_;

            //BatchReading = DefaultReadingPolicy;

            AsyncPolicy retryReading = Polly.Policy.Handle<Exception>()
                  .RetryForeverAsync((ex, i, c) => onRetry_(ex, TimeSpan.Zero, i, c));


            AsyncPolicy breaker = Polly.Policy
                .Handle<Exception>()
                            //.CircuitBreakerAsync(10, TimeSpan.FromSeconds(20), onBreak_, onReset_, onHalfOpen_);
                            .AdvancedCircuitBreakerAsync(
                                failureThreshold: 0.5, // Break on >=50% actions result in handled exceptions...
                                samplingDuration: TimeSpan.FromSeconds(20), // ... over any 10 second period
                                minimumThroughput: 8, // ... provided at least 8 actions in the 10 second period.
                                durationOfBreak: TimeSpan.FromSeconds(30), // Break for 30 seconds.
                                onBreak_,
                                onReset_,
                                onHalfOpen_);

            AsyncPolicy criticBreaker = Polly.Policy.Handle<InvalidOperationException>()
                                        .AdvancedCircuitBreakerAsync(
                                            failureThreshold: 0.9, // Break on >=n% actions result in handled exceptions...
                                            samplingDuration: TimeSpan.FromSeconds(10), // ... over any n second period
                                            minimumThroughput: 3, // ... provided at least n actions in the n second period.
                                            durationOfBreak: TimeSpan.FromMinutes(10));

            Policy = criticBreaker.WrapAsync(retryReading).WrapAsync(breaker);
        }

        #endregion // Ctor

        /// <summary>
        /// Gets or sets the batch reading policy.
        /// </summary>
        public AsyncPolicy Policy { get; set; }

        #region Cast overloads

        /// <summary>
        /// Performs an implicit conversion.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator ResiliencePolicies(AsyncPolicy policy)
        {
            return new ResiliencePolicies(policy);
        }

        /// <summary>
        /// Performs an implicit conversion.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator AsyncPolicy(ResiliencePolicies instance)
        {
            return instance.Policy;
        }

        #endregion // Cast overloads
    }
}