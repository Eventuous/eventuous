using System.Diagnostics.Tracing;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Subscriptions.Diagnostics;

[EventSource(Name = $"{DiagnosticName.BaseName}-subscription")]
public class SubscriptionsEventSource : EventSource {
    public static readonly SubscriptionsEventSource Log = new();

    const int MessageReceivedId                = 1;
    const int MessageIgnoredId                 = 2;
    const int MessageHandledId                 = 3;
    const int MessageHandlingFailedId          = 4;
    const int NoHandlerFoundId                 = 5;
    const int PayloadDeserializationFailedId   = 6;
    const int MetadataDeserializationFailedId  = 7;
    const int SubscriptionStartedId            = 8;
    const int SubscriptionStoppedId            = 9;
    const int SubscriptionDroppedId            = 10;
    const int SubscriptionResubscribingId      = 11;
    const int SubscriptionRestoredId           = 12;
    const int ResubscribeFailedId              = 13;
    const int FailedToHandleMessageWithRetryId = 20;
    const int CheckpointLoadedId               = 21;
    const int CheckpointStoredId               = 22;
    const int MessageSerializationNoResultId   = 23;
    const int ThrowOnErrorIncompatibleId       = 24;
    const int UnknownMessageTypeId             = 25;
    const int PartitionedFilterId              = 26;
    const int StoppingSomethingId              = 27;
    const int MetricCollectionFailedId         = 28;
    
    const int InfoId = 100;

    [NonEvent]
    public void MessageHandlingFailed(string handlerType, IBaseConsumeContext context, Exception? exception) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            MessageHandlingFailed(
                context.SubscriptionId,
                handlerType,
                context.MessageType,
                exception?.Message ?? "unknown error"
            );
    }

    [NonEvent]
    public void MessageIgnored(string handlerType, IBaseConsumeContext context) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            MessageIgnored(context.SubscriptionId, handlerType, context.MessageType);
    }

    [NonEvent]
    public void MessageHandled(string handlerType, IBaseConsumeContext context) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            MessageHandled(context.SubscriptionId, handlerType, context.MessageType);
    }

    [NonEvent]
    public void SubscriptionDropped(string subscriptionId, DropReason reason, Exception? e) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            SubscriptionDropped(subscriptionId, reason.ToString(), e?.ToString() ?? "unknown error");
    }

    [NonEvent]
    public void FailedToHandleMessageWithRetry(
        string    handlerType,
        string    messageType,
        int       retryCount,
        Exception exception
    ) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            FailedToHandleMessageWithRetry(handlerType, messageType, retryCount.ToString(), exception.ToString());
    }

    [NonEvent]
    public void CheckpointLoaded(ICheckpointStore store, Checkpoint checkpoint) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All))
            CheckpointLoaded(store.GetType().Name, checkpoint.Id, checkpoint.Position?.ToString() ?? "empty");
    }

    [NonEvent]
    public void CheckpointStored(ICheckpointStore store, Checkpoint checkpoint) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            CheckpointStored(store.GetType().Name, checkpoint.Id, checkpoint.Position?.ToString() ?? "empty");
    }

    [NonEvent]
    public void UnknownMessageType<T>() => UnknownMessageType(typeof(T).Name);

    [NonEvent]
    public void SendingMessageToPartition(IMessageConsumeContext context, long partition) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            SendingMessageToPartition(context.SubscriptionId, context.MessageType, partition.ToString());
    }

    [NonEvent]
    public void MessageReceived(IMessageConsumeContext context) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            MessageReceived(context.SubscriptionId, context.MessageType, context.Stream);
    }

    [NonEvent]
    public void MetricCollectionFailed(string metric, Exception exception)
        => MetricCollectionFailed(metric, exception.ToString());

    [Event(MessageReceivedId, Message = "[{0}] Received {1} from {2}", Level = EventLevel.Verbose)]
    public void MessageReceived(string subscriptionId, string messageType, string stream)
        => WriteEvent(MessageReceivedId, subscriptionId, messageType, stream);

    [Event(MessageIgnoredId, Message = "[{0}] {1} ignored {2}", Level = EventLevel.Verbose)]
    public void MessageIgnored(string id, string handlerType, string messageType)
        => WriteEvent(MessageIgnoredId, id, handlerType, messageType);

    [Event(MessageHandledId, Message = "[{0}] {1} handled {2}", Level = EventLevel.Verbose)]
    public void MessageHandled(string id, string handlerType, string eventType)
        => WriteEvent(MessageHandledId, id, handlerType, eventType);

    [Event(MessageHandlingFailedId, Message = "[{0}] {1} failed to process event {2}: {3}", Level = EventLevel.Error)]
    public void MessageHandlingFailed(string id, string handlerType, string messageType, string exception)
        => WriteEvent(MessageHandlingFailedId, id, handlerType, messageType, exception);

    [Event(NoHandlerFoundId, Message = "[{0}] No handler found for message {1}", Level = EventLevel.Warning)]
    public void NoHandlerFound(string handlerType, string messageType) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All)) WriteEvent(NoHandlerFoundId, handlerType, messageType);
    }

    [Event(MetricCollectionFailedId, Message = "Failed to collect metric {0}: {1}", Level = EventLevel.Warning)]
    public void MetricCollectionFailed(string metric, string exception)
        => WriteEvent(MetricCollectionFailedId, metric, exception);

    [Event(
        PayloadDeserializationFailedId,
        Message = "[{0}] Failed to deserialize event {1} {2} {3}: {4}",
        Level = EventLevel.Error
    )]
    public void PayloadDeserializationFailed(
        string subscriptionId,
        string stream,
        ulong  position,
        string type,
        string e
    ) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            WriteEvent(PayloadDeserializationFailedId, subscriptionId, stream, position.ToString(), type, e);
    }

    [Event(
        MetadataDeserializationFailedId,
        Message = "[{0}] Failed to deserialize metadata {1} {2}: {3}",
        Level = EventLevel.Error
    )]
    public void MetadataDeserializationFailed(
        string subscriptionId,
        string stream,
        ulong  position,
        string e
    ) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All))
            WriteEvent(MetadataDeserializationFailedId, subscriptionId, stream, position.ToString(), e);
    }

    [Event(SubscriptionStartedId, Message = "[{0}] Started", Level = EventLevel.Informational)]
    public void SubscriptionStarted(string subscriptionId) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All)) WriteEvent(SubscriptionStartedId, subscriptionId);
    }

    [Event(SubscriptionStoppedId, Message = "[{0}] Stopped", Level = EventLevel.Informational)]
    public void SubscriptionStopped(string subscriptionId) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All)) WriteEvent(SubscriptionStoppedId, subscriptionId);
    }

    [Event(SubscriptionDroppedId, Message = "[{0}] Dropped: {1} {2}", Level = EventLevel.Warning)]
    public void SubscriptionDropped(string subscriptionId, string reason, string e)
        => WriteEvent(SubscriptionDroppedId, subscriptionId, reason, e);

    [Event(SubscriptionRestoredId, Message = "[{0}] Restored", Level = EventLevel.Informational)]
    public void SubscriptionRestored(string subscriptionId) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All)) WriteEvent(SubscriptionRestoredId, subscriptionId);
    }

    [Event(SubscriptionResubscribingId, Message = "[{0}] Resubscribing", Level = EventLevel.Informational)]
    public void SubscriptionResubscribing(string subscriptionId) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All)) WriteEvent(SubscriptionResubscribingId, subscriptionId);
    }

    [Event(ResubscribeFailedId, Message = "[{0}] Unable to restart subscription: {1}", Level = EventLevel.Error)]
    public void ResubscribeFailed(string subscriptionId, string exception) {
        if (IsEnabled(EventLevel.Error, EventKeywords.All)) WriteEvent(ResubscribeFailedId, subscriptionId, exception);
    }

    [Event(
        FailedToHandleMessageWithRetryId,
        Message = "[{0}] Failed to handle {1} after {2} retries: {3}",
        Level = EventLevel.Warning
    )]
    public void FailedToHandleMessageWithRetry(
        string handlerType,
        string messageType,
        string retryCount,
        string exception
    )
        => WriteEvent(FailedToHandleMessageWithRetryId, handlerType, messageType, retryCount, exception);

    [Event(CheckpointLoadedId, Message = "[{0}] Loaded checkpoint {1}: {2}", Level = EventLevel.Informational)]
    public void CheckpointLoaded(string store, string checkpointId, string value)
        => WriteEvent(CheckpointLoadedId, store, checkpointId, value);

    [Event(CheckpointStoredId, Message = "[{0}] Stored checkpoint {1}: {2}", Level = EventLevel.Verbose)]
    public void CheckpointStored(string store, string checkpointId, string value)
        => WriteEvent(CheckpointStoredId, store, checkpointId, value);

    // The level of this message needs validation
    [Event(
        MessageSerializationNoResultId,
        Message = "[{0}] Message ignored as it didn't deserialize {1} {2} {3} {4}",
        Level = EventLevel.Verbose
    )]
    public void MessagePayloadInconclusive(
        string               subscriptionId,
        string               type,
        string               stream,
        string               contentType,
        DeserializationError reason
    ) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            WriteEvent(MessageSerializationNoResultId, subscriptionId, type, stream, contentType, reason);
    }

    [Event(
        ThrowOnErrorIncompatibleId,
        Message =
            "[{0}] Failure handler is set, but ThrowOnError is disabled, so the failure handler will never be called",
        Level = EventLevel.Warning
    )]
    public void ThrowOnErrorIncompatible(string subscriptionId)
        => WriteEvent(ThrowOnErrorIncompatibleId, subscriptionId);

    [Event(
        UnknownMessageTypeId,
        Message = "Message type {0} is not registered in the type map",
        Level = EventLevel.Warning
    )]
    public void UnknownMessageType(string type) => WriteEvent(UnknownMessageTypeId, type);

    [Event(PartitionedFilterId, Message = "[{0}] Sending {1} to partition {2}", Level = EventLevel.Verbose)]
    public void SendingMessageToPartition(string subscriptionId, string messageType, string partition)
        => WriteEvent(PartitionedFilterId, subscriptionId, messageType, partition);

    [Event(StoppingSomethingId, Message = "[{0}] Stopping {1} {2}", Level = EventLevel.Verbose)]
    public void Stopping(string id, string what, string name) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All)) WriteEvent(StoppingSomethingId, id, what, name);
    }
    
    [Event(InfoId, Message = "{0} {1} {2}", Level = EventLevel.Informational)]
    public void Info(string message, string? arg1 = null, string? arg2 = null) {
        if (IsEnabled(EventLevel.Informational, EventKeywords.All)) WriteEvent(InfoId, message, arg1, arg2);
    }
}
