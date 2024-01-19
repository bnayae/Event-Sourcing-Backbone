namespace EventSourcing.Backbone.Channels.RedisProvider.Common;

public static class RedisChannelConstants
{
    public const string CHANNEL_TYPE = "REDIS Channel V1";
    public const string META_ARRAY_SEPARATOR = "~|~";

    public const string END_POINT_KEY = "REDIS_EVENT_SOURCE_ENDPOINT";
    public const string PASSWORD_KEY = "REDIS_EVENT_SOURCE_PASS";

    public const string STORE_KEY = "__REDIS_STORE__";

    /// <summary>
    /// a work around used to release messages back to the stream (consumer)
    /// </summary>
    public const string NONE_CONSUMER = "__NONE_CUNSUMER__";

    public static class MetaKeys
    {
        public const string SegmentsKeys = "segments-keys";
        public const string InterceptorsKeys = "interceptors-keys";
        public const string TelemetryBaggage = "telemetry-baggage";
        public const string TelemetrySpanId = "telemetry-span-id";
        public const string TelemetryTraceId = "telemetry-trace-id";
    }

}

