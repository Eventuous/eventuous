using System.Collections.Concurrent;

namespace Eventuous.Subscriptions;

public record struct EventHandlingResult {
    EventHandlingResult(EventHandlingStatus status, string handlerType, Exception? exception = null) {
        Status      = status;
        Exception   = exception;
        HandlerType = handlerType;
    }

    public static EventHandlingResult Succeeded(string handlerType)
        => new(EventHandlingStatus.Success, handlerType);

    public static EventHandlingResult Ignored(string handlerType)
        => new(EventHandlingStatus.Ignored, handlerType);

    public static EventHandlingResult Failed(string handlerType, Exception? e)
        => new(EventHandlingStatus.Failure, handlerType, e);

    public EventHandlingStatus Status      { get; }
    public Exception?          Exception   { get; }
    public string              HandlerType { get; }
}

public class HandlingResults {
    readonly ConcurrentBag<EventHandlingResult> _results = new();

    EventHandlingStatus _handlingStatus = 0;

    public void Add(EventHandlingResult result) {
        if (_results.Any(x => x.HandlerType == result.HandlerType)) return;

        _handlingStatus |= result.Status;
        _results.Add(result);
    }

    public IEnumerable<EventHandlingResult> GetResultsOf(EventHandlingStatus status)
        => _results.Where(x => x.Status == status);

    public bool ReportedBy(string handlerType) => _results.Any(x => x.HandlerType == handlerType);

    public EventHandlingStatus GetFailureStatus() => _handlingStatus & EventHandlingStatus.Handled;

    public EventHandlingStatus GetIgnoreStatus() => _handlingStatus & EventHandlingStatus.Ignored;
    
    public bool IsPending() => _handlingStatus == 0;

    public Exception? GetException() => _results.First(x => x.Exception != null).Exception;
}