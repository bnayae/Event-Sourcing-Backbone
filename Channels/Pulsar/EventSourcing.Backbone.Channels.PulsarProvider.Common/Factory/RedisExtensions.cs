using System.Reflection;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using StackExchange.Redis;

namespace EventSourcing.Backbone;

/// <summary>
/// REDIS client factory
/// </summary>
public static class RedisExtensions
{
    private static readonly AssemblyName ASSEMBLY = Assembly.GetEntryAssembly()?.GetName() ?? new AssemblyName("EventSourcing");
    public static IConnectionMultiplexer WithTelemetry(this IConnectionMultiplexer connection)
    {
        string enable = Environment.GetEnvironmentVariable("EVENT_SOURCE_WITH_REDIS_TRACE") ?? "false";
        if (bool.TryParse(enable, out var enableValue) && enableValue)
        {
            Sdk.CreateTracerProviderBuilder()
                            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                                        .AddService("evt-src-redis"))
                            .AddRedisInstrumentation(
                            connection,
                            opt =>
                            {
                                opt.SetVerboseDatabaseStatements = false;
                                opt.FlushInterval = TimeSpan.FromSeconds(5);
                                opt.Enrich = (activity, command) =>
                                                {
                                                    activity.SetTag("app.version", ASSEMBLY.Version);
                                                    activity.SetTag("app.name", ASSEMBLY.Name);
                                                };
                            })
                            .AddOtlpExporter()
                            .Build();
        }
        return connection;
    }
}
