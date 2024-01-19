using static EventSourcing.Backbone.Channels.RedisProvider.Common.RedisChannelConstants;


namespace EventSourcing.Backbone
{
    /// <summary>
    /// Environment keys for REDIS's credentials
    /// </summary>
    public record RedisCredentialsEnvKeys(string Endpoint = END_POINT_KEY, string Password = PASSWORD_KEY) : IRedisCredentials;
}
