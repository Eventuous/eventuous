using System.Diagnostics.Tracing;
using Eventuous.Diagnostics;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Subscriptions.Diagnostics;

[EventSource(Name = $"{DiagnosticName.BaseName}-subscription")]
public class SubscriptionsEventSource : EventSource {
    public static readonly SubscriptionsEventSource Log = new();

    [NonEvent]
    public void FailedToHandleMessage(Type? handlerType, string messageType, Exception? exception) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            FailedToHandleMessage(
                handlerType?.Name ?? "unknown",
                messageType,
                exception?.ToString() ?? "unknown error"
            );
    }

    [NonEvent]
    public void SubscriptionDropped(string subscriptionId, DropReason reason, Exception? e) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            SubscriptionDropped(subscriptionId, reason.ToString(), e?.ToString() ?? "unknown error");
    }
    
    [Event(1, Message = "Handler {0} failed to process event {1}: {2}", Level = EventLevel.Error)]
    public void FailedToHandleMessage(string handlerType, string messageType, string exception)
        => WriteEvent(1, handlerType, messageType, exception);

    [Event(2, Message = "[{0}] Received {1}", Level = EventLevel.Verbose)]
    public void ReceivedMessage(string subscriptionId, string messageType) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            WriteEvent(2, subscriptionId, messageType);
    }

    [Event(3, Message = "[{0}] Failed to deserialize event {1} {2} {3}: {4}", Level = EventLevel.Error)]
    public void PayloadDeserializationFailed(
        string subscriptionId,
        string stream,
        ulong  position,
        string type,
        string e
    ) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            WriteEvent(3, subscriptionId, stream, position.ToString(), type, e);
    }

    [Event(4, Message = "[{0}] Dropped: {1} {2}", Level = EventLevel.Warning)]
    public void SubscriptionDropped(string subscriptionId, string reason, string e)
        => WriteEvent(4, subscriptionId, reason, e);

    [Event(5, Message = "[{0}] Resubscribing", Level = EventLevel.Informational)]
    public void SubscriptionResubscribing(string subscriptionId) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            WriteEvent(5, subscriptionId);
    }

    [Event(6, Message = "[{0}] Unable to restart subscription: {1}", Level = EventLevel.Error)]
    public void ResubscribeFailed(string subscriptionId, string exception) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            WriteEvent(6, subscriptionId, exception);
    }

    [Event(7, Message = "[{0}] Restored", Level = EventLevel.Informational)]
    public void SubscriptionRestored(string subscriptionId) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            WriteEvent(7, subscriptionId);
    }

    [Event(8, Message = "[{0}] Failed to deserialize metadata {1} {2}: {3}", Level = EventLevel.Error)]
    public void MetadataDeserializationFailed(
        string subscriptionId,
        string stream,
        ulong  position,
        string e
    ) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            WriteEvent(8, subscriptionId, stream, position.ToString(), e);
    }

    [Event(9, Message = "[{0}] No handler found for message {1}", Level = EventLevel.Warning)]
    public void NoHandlerFound(string handlerType, string messageType) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            WriteEvent(9, handlerType, messageType);
    }

    [Event(10, Message = "[{0}] Started", Level = EventLevel.Informational)]
    public void SubscriptionStarted(string subscriptionId) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            WriteEvent(10, subscriptionId);
    }
    
    [Event(11, Message = "[{0}] Stopped", Level = EventLevel.Informational)]
    public void SubscriptionStopped(string subscriptionId) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            WriteEvent(11, subscriptionId);
    }
    
    [Event(12, Message = "[{0}] Event {1} ignored by projection", Level = EventLevel.Verbose)]
    public void EventIgnoredByProjection(string handlerType, string eventType) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            WriteEvent(12, handlerType, eventType);
    }

    [Event(13, Message = "[{0}] Event {1} being projected", Level = EventLevel.Verbose)]
    public void EventHandledByProjection(string handlerType, string eventType) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            WriteEvent(13, handlerType, eventType);
    }

    [Event(100, Message = "{0} {1} {2}", Level = EventLevel.Informational)]
    public void Info(string message, string? arg1 = null, string? arg2 = null) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All)) WriteEvent(100, message, arg1, arg2);
    }

    [Event(101, Message = "{0} {1} {2}", Level = EventLevel.Warning)]
    public void Warn(string message, string? arg1 = null, string? arg2 = null) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All)) WriteEvent(101, message, arg1, arg2);
    }
}