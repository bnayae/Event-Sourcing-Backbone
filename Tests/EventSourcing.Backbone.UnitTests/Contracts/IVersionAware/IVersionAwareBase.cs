﻿namespace EventSourcing.Backbone.UnitTests;

/// <summary>
/// Test contract
/// </summary>
public interface IVersionAwareBase
{
    ValueTask ExecuteAsync(string key, int value);
    [EventSourceVersion(1)]
    ValueTask ExecuteAsync(int value);
    [EventSourceVersion(2)]
    ValueTask ExecuteAsync(DateTime value);
    [EventSourceVersion(3)]
    ValueTask ExecuteAsync(string value);
    [EventSourceVersion(4)]
    [EventSourceDeprecate(EventsContractType.Consumer, Date = "2023-08-02", Remark = "For testing")]
    ValueTask ExecuteAsync(TimeSpan value);
    [EventSourceVersion(3)]
    ValueTask NotIncludesAsync(string value);
}
