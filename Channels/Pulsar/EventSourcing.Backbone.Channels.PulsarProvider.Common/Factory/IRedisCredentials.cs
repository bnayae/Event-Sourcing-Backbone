namespace EventSourcing.Backbone
{
    /// <summary>
    /// Redis credentials abstraction
    /// </summary>
    public interface IRedisCredentials
    {
        string? Endpoint { get; }
        string? Password { get; }
    }
}