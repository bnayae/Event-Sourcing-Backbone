namespace EventSourcing.Backbone
{
    /// <summary>
    /// Raw keys for REDIS's credentials 
    /// </summary>
    public record RedisCredentialsRaw : IRedisCredentials
    {
        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisCredentialsRaw"/> class.
        /// </summary>
        /// <param name="endpoint">The raw endpoint (not an environment variable).</param>
        /// <param name="password">The password (not an environment variable).</param>
        /// <exception cref="System.ArgumentNullException">endpoint</exception>
        public RedisCredentialsRaw(string endpoint, string? password = null)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            Password = password;
        }

        #endregion // Ctor

        /// <summary>
        /// The raw endpoint (not an environment variable)
        /// </summary>
        public string? Endpoint { get; }
        /// <summary>
        /// The password (not an environment variable).
        /// </summary>
        public string? Password { get; }
    }
}
