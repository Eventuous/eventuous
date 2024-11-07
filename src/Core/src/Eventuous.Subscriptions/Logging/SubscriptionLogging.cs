// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.Logging;

using Context;

public static class LoggingExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MessageReceived(this LogContext log, IMessageConsumeContext context)
        => log.TraceLog?.Log(
            "Received {MessageType} from {Stream}:{Position} seq {Sequence}",
            context.MessageType,
            context.Stream,
            context.GlobalPosition,
            context.Sequence
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MessageHandled(this LogContext log, string handlerType, IBaseConsumeContext context)
        => log.TraceLog?.Log(
            "{Handler} handled {MessageType} {Stream}:{Position} seq {Sequence}",
            handlerType,
            context.MessageType,
            context.Stream,
            context.GlobalPosition,
            context.Sequence
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MessageIgnored(this LogContext log, string handlerType, IBaseConsumeContext context)
        => log.TraceLog?.Log(
            "{Handler} ignored {MessageType} {Stream}:{Position} seq {Sequence}",
            handlerType,
            context.MessageType,
            context.Stream,
            context.GlobalPosition,
            context.Sequence
        );

    public static void MessageIgnoredWhenStopping(this LogContext log, Exception e) =>
        log.DebugLog?.Log("Message ignored because subscription is stopping: {Message}", e.Message);

    public static void MessageTypeNotFound<T>(this ILogger? log)
        => log?.LogWarning("Message type {MessageType} not registered in the type map", typeof(T).Name);

    public static void MessageHandlerNotFound(this LogContext log, string handler, string messageType)
        => log.WarnLog?.Log("No handler found in {Handler} for message type {MessageType}", handler, messageType);

    public static void MessageHandlingFailed(this LogContext log, string handlerType, IBaseConsumeContext context, Exception? exception)
        => log.ErrorLog?.Log(exception, "Message handling failed at {HandlerType} for message {MessageId}", handlerType, context.MessageId);

    public static void PayloadDeserializationFailed(this LogContext log, string stream, ulong position, string messageType, Exception exception)
        => log.ErrorLog?.Log(exception, "Failed to deserialize event {MessageType} at {Stream}:{Position}", messageType, stream, position);

    public static void MetadataDeserializationFailed(this LogContext log, string stream, ulong position, Exception exception)
        => log.ErrorLog?.Log(exception, "Failed to deserialize metadata at {Stream}:{Position}", stream, position);

    public static void MessagePayloadInconclusive(this LogContext log, string messageType, string stream, DeserializationError error)
        => log.DebugLog?.Log("Message of type {MessageType} from {Stream} ignored as it didn't deserialize: {Error}", messageType, stream, error);

    public static void ThrowOnErrorIncompatible(this LogContext log)
        => log.WarnLog?.Log("Failure handler is set, but ThrowOnError is disabled, so the failure handler will never be called");

    public static void FailedToHandleMessageWithRetry(this LogContext log, string handlerType, string messageType, int retryCount, Exception exception)
        => log.ErrorLog?.Log(
            exception,
            "Failed to handle message {MessageType} with {HandlerType} after {RetryCount} retries",
            messageType,
            handlerType,
            retryCount
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MessageAcked(this LogContext log, string messageType, ulong position)
        => log.TraceLog?.Log("Message {Type} acknowledged at {Position}", messageType, position);

    public static void MessageNacked(this LogContext log, string messageType, ulong position, Exception exception)
        => log.WarnLog?.Log(exception, "Message {Type} not acknowledged at {Position}", messageType, position);

    public static void SubscriptionStarted(this LogContext log) => log.InfoLog?.Log("Started");

    public static void SubscriptionStopped(this LogContext log) => log.InfoLog?.Log("Stopped");

    public static void SubscriptionDropped(this LogContext log, DropReason reason, Exception? exception)
        => log.WarnLog?.Log(exception, "Dropped: {Reason}", reason);

    public static void SubscriptionWillResubscribe(this LogContext log, TimeSpan delay) => log.WarnLog?.Log($"Will resubscribe after {delay}");

    public static void SubscriptionResubscribing(this LogContext log) => log.WarnLog?.Log("Resubscribing");

    public static void SubscriptionResubscribed(this LogContext log) => log.InfoLog?.Log("Resubscribed");

    public static void SubscriptionResubscribeFailed(this LogContext log, Exception e) => log.ErrorLog?.Log(e, "Failed to resubscribe");
}
