using System.Collections.Immutable;
using System.Diagnostics;

namespace EventSourcing.Backbone.SrcGen.Entities;

[DebuggerDisplay("{Type}, Min: {MinVersion}, naming: {VersionNaming}")]
internal struct VersionInfo
{
    public VersionInfo(EventsContractType type)
    {
        Type = type;
        VersionNaming = VersionNaming.Default;
        MinVersion = 0;
        IgnoreVersion = ImmutableHashSet<int>.Empty;
    }

    public EventsContractType Type { get; private set; }

    /// <summary>
    /// Version naming convention.
    /// </summary>
    public VersionNaming VersionNaming { get; set; }

    /// <summary>
    /// Won't generate method with version lower than this value
    /// </summary>
    public int MinVersion { get; set; }

    /// <summary>
    /// Won't generate method with versions specified
    /// </summary>
    public IImmutableSet<int> IgnoreVersion { get; set; }

    public string FormatNameWithVersion(string name, int version)
    {
        string versionSuffix = VersionNaming switch
        {
            SrcGen.Entities.VersionNaming.Append => version.ToString(),
            SrcGen.Entities.VersionNaming.AppendUnderscore => $"_{version}",
            _ => string.Empty
        };

        if (name.EndsWith("Async"))
        {
            var prefix = name.Substring(0, name.Length - 5);
            return $"{prefix}{versionSuffix}Async";
        }
        return $"{name}{versionSuffix}";
    }
}
