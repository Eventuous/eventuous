// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Logging;

public static class LoggingExtensions {
    public static void MessageReceived(this LogContext log, IMessageConsumeContext context)
        => log.TraceLog?.Log(
            "Received {MessageType} from {Stream}:{Position} seq {Sequence}",
            context.MessageType,
            context.Stream,
            context.GlobalPosition,
            context.Sequence
        );

    public static void MessageHandled(this LogContext log, string handlerType, IBaseConsumeContext context)
        => log.DebugLog?.Log(
            "{Handler} handled {MessageType} {Stream}:{Position} seq {Sequence}",
            handlerType,
            context.MessageType,
            context.Stream,
            context.GlobalPosition,
            context.Sequence
        );

    public static void MessageIgnored(this LogContext log, string handlerType, IBaseConsumeContext context)
        => log.DebugLog?.Log(
            "{Handler} ignored {MessageType} {Stream}:{Position} seq {Sequence}",
            handlerType,
            context.MessageType,
            context.Stream,
            context.GlobalPosition,
            context.Sequence
        );

    public static void MessageTypeNotFound<T>(this LogContext log)
        => log.WarnLog?.Log("Message type {MessageType} not registered in the type map", typeof(T).Name);

    public static void MessageHandlerNotFound(this LogContext log, string handler, string messageType)
        => log.WarnLog?.Log("No handler found in {Handler} for message type {MessageType}", handler, messageType);

    public static void MessageHandlingFailed(
        this LogContext     log,
        string              handlerType,
        IBaseConsumeContext context,
        Exception?          exception
    )
        => log.ErrorLog?.Log(
            exception,
            "Message handling failed at {HandlerType} with message {MessageId}",
            handlerType,
            context.MessageId
        );

    public static void PayloadDeserializationFailed(
        this LogContext log,
        string          stream,
        ulong           position,
        string          messageType,
        Exception       exception
    )
        => log.ErrorLog?.Log(
            exception,
            "Failed to deserialize event {MessageType} at {Stream}:{Position}",
            messageType,
            stream,
            position
        );

    public static void MetadataDeserializationFailed(
        this LogContext log,
        string          stream,
        ulong           position,
        Exception       exception
    )
        => log.ErrorLog?.Log(
            exception,
            "Failed to deserialize metadata at {Stream}:{Position}",
            stream,
            position
        );

    public static void MessagePayloadInconclusive(
        this LogContext      log,
        string               messageType,
        string               stream,
        DeserializationError error
    )
        => log.DebugLog?.Log(
            "Message of type {MessageType} from {Stream} ignored as it didn't deserialize: {Error}",
            messageType,
            stream,
            error
        );

    public static void ThrowOnErrorIncompatible(this LogContext log)
        => log.WarnLog?.Log(
            "Failure handler is set, but ThrowOnError is disabled, so the failure handler will never be called"
        );

    public static void FailedToHandleMessageWithRetry(
        this LogContext log,
        string          handlerType,
        string          messageType,
        int             retryCount,
        Exception       exception
    )
        => log.ErrorLog?.Log(
            exception,
            "Failed to handle message {MessageType} with {HandlerType} after {RetryCount} retries",
            messageType,
            handlerType,
            retryCount
        );
}
