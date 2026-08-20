// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.Logging;

using Context;

public static class LoggingExtensions {
    extension(LogContext log) {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MessageReceived(IMessageConsumeContext context)
            => log.TraceLog?.Log(
                "Received {MessageType} from {Stream}:{Position} seq {Sequence}",
                context.MessageType,
                context.Stream,
                context.GlobalPosition,
                context.Sequence
            );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MessageHandled(string handlerType, IBaseConsumeContext context)
            => log.TraceLog?.Log(
                "{Handler} handled {MessageType} {Stream}:{Position} seq {Sequence}",
                handlerType,
                context.MessageType,
                context.Stream,
                context.GlobalPosition,
                context.Sequence
            );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MessageIgnored(string handlerType, IBaseConsumeContext context)
            => log.TraceLog?.Log(
                "{Handler} ignored {MessageType} {Stream}:{Position} seq {Sequence}",
                handlerType,
                context.MessageType,
                context.Stream,
                context.GlobalPosition,
                context.Sequence
            );

        public void MessageIgnoredWhenStopping(Exception e) =>
            log.DebugLog?.Log("Message ignored because subscription is stopping: {Message}", e.Message);

        public void MessageHandlerNotFound(string handler, string messageType)
            => log.WarnLog?.Log("No handler found in {Handler} for message type {MessageType}", handler, messageType);

        public void MessageHandlingFailed(string handlerType, IBaseConsumeContext context, Exception? exception)
            => log.ErrorLog?.Log(exception, "Message handling failed at {HandlerType} for message {MessageId}", handlerType, context.MessageId);

        public void PayloadDeserializationFailed(string stream, ulong position, string messageType, Exception exception)
            => log.ErrorLog?.Log(exception, "Failed to deserialize event {MessageType} at {Stream}:{Position}", messageType, stream, position);

        public void MetadataDeserializationFailed(string stream, ulong position, Exception exception)
            => log.ErrorLog?.Log(exception, "Failed to deserialize metadata at {Stream}:{Position}", stream, position);

        public void MessagePayloadInconclusive(string messageType, string stream, DeserializationError error)
            => log.DebugLog?.Log("Message of type {MessageType} from {Stream} ignored as it didn't deserialize: {Error}", messageType, stream, error);

        public void ThrowOnErrorIncompatible()
            => log.WarnLog?.Log("Failure handler is set, but ThrowOnError is disabled, so the failure handler will never be called");

        public void FailedToHandleMessageWithRetry(string handlerType, string messageType, int retryCount, Exception exception)
            => log.ErrorLog?.Log(
                exception,
                "Failed to handle message {MessageType} with {HandlerType} after {RetryCount} retries",
                messageType,
                handlerType,
                retryCount
            );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MessageAcked(string messageType, ulong position)
            => log.TraceLog?.Log("Message {Type} acknowledged at {Position}", messageType, position);

        /// <summary>
        /// A message from a dropped run that finished after its replacement started; unacked, so the new
        /// run redelivers it from the checkpoint.
        /// </summary>
        public void MessageFromPreviousRunIgnored(IBaseConsumeContext context)
            => log.DebugLog?.Log(
                "Message {MessageType} from {Stream}:{Position} belongs to a previous run and was not acknowledged",
                context.MessageType,
                context.Stream,
                context.GlobalPosition
            );

        /// <summary>
        /// The handling worker is stopping and never took the message; left unacknowledged, so it comes
        /// back on the next run.
        /// </summary>
        public void MessageNotQueued(IBaseConsumeContext context)
            => log.WarnLog?.Log(
                "Message {MessageType} from {Stream}:{Position} was not queued for handling because the subscription is stopping",
                context.MessageType,
                context.Stream,
                context.GlobalPosition
            );

        public void MessageNacked(string messageType, ulong position, Exception exception)
            => log.WarnLog?.Log(exception, "Message {Type} not acknowledged at {Position}", messageType, position);

        public void SubscriptionStarted() => log.InfoLog?.Log("Started");
        public void SubscriptionStopped() => log.InfoLog?.Log("Stopped");

        public void SubscriptionDropped(DropReason reason, Exception? exception)
            => log.WarnLog?.Log(exception, "Dropped: {Reason}", reason);

        public void SubscriptionWillResubscribe(TimeSpan delay) => log.WarnLog?.Log($"Will resubscribe after {delay}");

        /// <summary>
        /// Configured retry delay can't be waited on; fell back to the default. Otherwise this
        /// misconfiguration would only show up as a subscription retrying flat out.
        /// </summary>
        public void SubscriptionRetryDelayInvalid(TimeSpan configured, TimeSpan used)
            => log.WarnLog?.Log($"Retry delay {configured} cannot be waited on, using {used} instead");

        /// <summary>
        /// Configured teardown timeout can't be waited on; fell back to the default. Otherwise this
        /// misconfiguration would only show up as a teardown that never gives up.
        /// </summary>
        public void SubscriptionTeardownTimeoutInvalid(TimeSpan configured, TimeSpan used)
            => log.WarnLog?.Log($"Teardown timeout {configured} cannot be waited on, using {used} instead");

        /// <summary>
        /// The caller stopped waiting for the subscription to finish stopping. Not a failed stop — teardown
        /// runs on its own budget regardless.
        /// </summary>
        public void SubscriptionStopTimedOut() => log.WarnLog?.Log("Gave up waiting for the subscription to stop");

        public void SubscriptionResubscribing() => log.WarnLog?.Log("Resubscribing");
        public void SubscriptionResubscribed() => log.InfoLog?.Log("Resubscribed");

        /// <summary>
        /// The supervisor itself failed, leaving the subscription down for good — anything else it sees is
        /// reported as a drop and retried.
        /// </summary>
        public void SubscriptionSuperviseFailed(Exception e) => log.ErrorLog?.Log(e, "Subscription supervisor failed");

        public void SubscriptionDisconnectFailed(Exception e) => log.WarnLog?.Log(e, "Failed to release the subscription");

        public void SubscriptionCallbackFailed(Exception e) => log.WarnLog?.Log(e, "Subscription callback failed");
    }

    public static void MessageTypeNotFound<T>(this ILogger? log)
        => log?.LogWarning("Message type {MessageType} not registered in the type map", typeof(T).Name);
}
