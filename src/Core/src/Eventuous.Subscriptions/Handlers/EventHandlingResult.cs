using System.Collections.Concurrent;

namespace Eventuous.Subscriptions;

public record EventHandlingResult {
    EventHandlingResult(EventHandlingStatus status, Type? handlerType, Exception? exception = null) {
        Status      = status;
        Exception   = exception;
        HandlerType = handlerType;
    }

    public static EventHandlingResult Succeeded(Type? handlerType)
        => new(EventHandlingStatus.Success, handlerType);

    public static EventHandlingResult Ignored(Type? handlerType)
        => new(EventHandlingStatus.Ignored, handlerType);

    public static EventHandlingResult Failed(Type? handlerType, Exception? e)
        => new(EventHandlingStatus.Failure, handlerType, e);

    public EventHandlingStatus Status      { get; }
    public Exception?          Exception   { get; }
    public Type?               HandlerType { get; }
}

public class HandlingResults {
    readonly ConcurrentBag<EventHandlingResult> _results = new();

    EventHandlingStatus _handlingStatus = 0;

    public void Add(EventHandlingResult result) {
        if (_results.Any(x => x.HandlerType == result.HandlerType)) return;

        _handlingStatus |= result.Status;
        _results.Add(result);
    }

    public EventHandlingStatus GetFailureStatus() => _handlingStatus & EventHandlingStatus.Handled;

    public EventHandlingStatus GetIgnoreStatus() => _handlingStatus & EventHandlingStatus.Ignored;

    public Exception GetException() => _results.First(x => x.Exception != null).Exception!;
}