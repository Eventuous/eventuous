// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

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
    EventHandlingResult? _singleResult;
    List<EventHandlingResult>? _multipleResults;
    EventHandlingStatus _handlingStatus;

    public void Add(EventHandlingResult result) {
        // Single result case (most common - optimized for zero allocation)
        if (_singleResult == null && _multipleResults == null) {
            _singleResult = result;
            _handlingStatus = result.Status;
            return;
        }

        // Transition to multiple results
        if (_multipleResults == null && _singleResult != null) {
            _multipleResults = [_singleResult.Value];
            _singleResult = null;
        }

        // Check for duplicate handler (manual iteration to avoid LINQ allocation)
        for (int i = 0; i < _multipleResults!.Count; i++) {
            if (_multipleResults[i].HandlerType == result.HandlerType) return;
        }

        _handlingStatus |= result.Status;
        _multipleResults.Add(result);
    }

    public IEnumerable<EventHandlingResult> GetResultsOf(EventHandlingStatus status) {
        if (_singleResult != null) {
            if (_singleResult.Value.Status == status) {
                yield return _singleResult.Value;
            }
        }
        else if (_multipleResults != null) {
            for (int i = 0; i < _multipleResults.Count; i++) {
                if (_multipleResults[i].Status == status) {
                    yield return _multipleResults[i];
                }
            }
        }
    }

    public EventHandlingStatus GetFailureStatus() => _handlingStatus & EventHandlingStatus.Handled;

    public EventHandlingStatus GetIgnoreStatus() => _handlingStatus & EventHandlingStatus.Ignored;

    public bool IsPending() => _handlingStatus == 0;

    public Exception? GetException() {
        if (_singleResult != null) {
            return _singleResult.Value.Exception;
        }

        if (_multipleResults != null) {
            for (int i = 0; i < _multipleResults.Count; i++) {
                if (_multipleResults[i].Exception != null) {
                    return _multipleResults[i].Exception;
                }
            }
        }

        return null;
    }
}
