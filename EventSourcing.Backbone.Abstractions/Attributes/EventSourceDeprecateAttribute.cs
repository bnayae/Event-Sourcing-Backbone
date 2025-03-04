﻿namespace EventSourcing.Backbone;

/// <summary>
/// Event source's version deprecation.
/// Used to retired an API version
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class EventSourceDeprecateAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventsContractAttribute" /> class.
    /// </summary>
    /// <param name="type">The event target type.</param>
    public EventSourceDeprecateAttribute(EventsContractType type)
    {
        Type = type;
    }

    /// <summary>
    /// Gets the target type.
    /// </summary>
    public EventsContractType Type { get; }

    /// <summary>
    /// Describe the deprecation reason
    /// </summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// Document the date of deprecation, recommended format is: yyyy-MM-dd
    /// </summary>
    public string Date { get; set; } = string.Empty;
}
