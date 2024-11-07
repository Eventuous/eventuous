// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Eventuous.Subscriptions;

[StructLayout(LayoutKind.Auto)]
public readonly record struct EventHandlingResult(EventHandlingStatus Status, string HandlerType, Exception? Exception = null) {
    public static EventHandlingResult Succeeded(string handlerType) => new(EventHandlingStatus.Success, handlerType);

    public static EventHandlingResult Ignored(string handlerType) => new(EventHandlingStatus.Ignored, handlerType);

    public static EventHandlingResult Failed(string handlerType, Exception? e) => new(EventHandlingStatus.Failure, handlerType, e);

    public EventHandlingStatus Status      { get; } = Status;
    public Exception?          Exception   { get; } = Exception;
    public string              HandlerType { get; } = HandlerType;
}

public class HandlingResults {
    readonly ConcurrentBag<EventHandlingResult> _results = [];

    EventHandlingStatus _handlingStatus = 0;

    public void Add(EventHandlingResult result) {
        if (_results.Any(x => x.HandlerType == result.HandlerType)) return;

        _handlingStatus |= result.Status;
        _results.Add(result);
    }

    public IEnumerable<EventHandlingResult> GetResultsOf(EventHandlingStatus status) => _results.Where(x => x.Status == status);

    public EventHandlingStatus GetFailureStatus() => _handlingStatus & EventHandlingStatus.Handled;

    public EventHandlingStatus GetIgnoreStatus() => _handlingStatus & EventHandlingStatus.Ignored;

    public bool IsPending() => _handlingStatus == 0;

    public Exception? GetException() => _results.FirstOrDefault(x => x.Exception != null).Exception;
}
