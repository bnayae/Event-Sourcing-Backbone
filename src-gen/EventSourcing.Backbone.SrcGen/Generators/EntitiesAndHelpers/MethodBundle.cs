﻿using System.Diagnostics;

using EventSourcing.Backbone.SrcGen.Entities;

using Microsoft.CodeAnalysis;

namespace EventSourcing.Backbone.SrcGen.Generators.EntitiesAndHelpers;

/// <summary>
/// Method Bundle
/// </summary>
[DebuggerDisplay("{Name}, {Version}, {Parameters}, Deprecated: {Deprecated}")]
internal sealed class MethodBundle : IFormattable
{
    public MethodBundle(
        IMethodSymbol method,
        string name,
        string fullName,
        int version,
        VersionNaming versionNaming,
        string parameters,
        bool deprecated)
    {
        Method = method;
        Name = name;
        FullName = fullName;
        Version = version;
        VersionNaming = versionNaming;
        Parameters = parameters;
        Deprecated = deprecated;
    }

    public IMethodSymbol Method { get; }
    public string Name { get; }
    public string FullName { get; }
    public int Version { get; }
    public VersionNaming VersionNaming { get; }
    public string Parameters { get; }
    public bool Deprecated { get; }

    public override string ToString() => $"{Name}_{Version}_{Parameters.Replace(",", "_")}";

    string IFormattable.ToString(string format, IFormatProvider formatProvider)
    {
        string fmt = format ?? "_";
        return $"{FullName}{fmt}{Version}{fmt}{Parameters.Replace(",", fmt)}";
    }

    public string FormatMethodFullName(string? nameOverride = null)
    {
        string name = nameOverride ?? FullName;
        string versionSuffix = VersionNaming switch
        {
            SrcGen.Entities.VersionNaming.Append => Version.ToString(),
            SrcGen.Entities.VersionNaming.AppendUnderscore => $"_{Version}",
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
