// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Logging;
using ActivityStatus = Eventuous.Diagnostics.ActivityStatus;

namespace Eventuous.Subscriptions.Context;

public static class ContextResultExtensions {
    /// <summary>
    /// Allows to acknowledge the message by a specific handler, identified by a string
    /// </summary>
    /// <param name="context">Consume context</param>
    /// <param name="handlerType">Handler type identifier</param>
    public static void Ack(this IBaseConsumeContext context, string handlerType) {
        context.HandlingResults.Add(EventHandlingResult.Succeeded(handlerType));
        context.LogContext.MessageHandled(handlerType, context);
    }

    /// <summary>
    /// Allows to convey the message handling failure that occurred in a specific handler
    /// </summary>
    /// <param name="context">Message context</param>
    /// <param name="handlerType">Handler type identifier</param>
    /// <param name="exception">Optional: handler exception</param>
    public static void Nack(this IBaseConsumeContext context, string handlerType, Exception? exception) {
        context.HandlingResults.Add(EventHandlingResult.Failed(handlerType, exception));
        if (exception is not TaskCanceledException)
            context.LogContext.MessageHandlingFailed(handlerType, context, exception);

        if (Activity.Current != null && Activity.Current.Status != ActivityStatusCode.Error) {
            Activity.Current.SetActivityStatus(
                ActivityStatus.Error(exception, $"Error handling {context.MessageType}")
            );
        }
    }

    /// <summary>
    /// Allows to convey the fact that the message was ignored by the handler
    /// </summary>
    /// <param name="context">Consume context</param>
    /// <param name="handlerType">Handler type identifier</param>
    public static void Ignore(this IBaseConsumeContext context, string handlerType) {
        context.HandlingResults.Add(EventHandlingResult.Ignored(handlerType));
        context.LogContext.MessageIgnored(handlerType, context);
    }

    /// <summary>
    /// Allows to acknowledge the message by a specific handler, identified by a string
    /// </summary>
    /// <param name="context">Consume context</param>
    /// <typeparam name="T">Handler type</typeparam>
    public static void Ack<T>(this IBaseConsumeContext context) => context.Ack(typeof(T).Name);

    /// <summary>
    /// Allows to convey the fact that the message was ignored by the handler
    /// </summary>
    /// <param name="context">Consume context</param>
    /// <typeparam name="T">Handler type</typeparam>
    public static void Ignore<T>(this IBaseConsumeContext context) => context.Ignore(typeof(T).Name);

    /// <summary>
    /// Allows to convey the message handling failure that occurred in a specific handler
    /// </summary>
    /// <param name="context">Consume context</param>
    /// <param name="exception">Optional: handler exception</param>
    /// <typeparam name="T">Handler type</typeparam>
    public static void Nack<T>(this IBaseConsumeContext context, Exception? exception)
        => context.Nack(typeof(T).Name, exception);

    /// <summary>
    /// Returns true if the message was ignored by all handlers
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WasIgnored(this IBaseConsumeContext context) {
        var status       = context.HandlingResults.GetIgnoreStatus();
        var handleStatus = context.HandlingResults.GetFailureStatus();

        return (status & EventHandlingStatus.Ignored) == EventHandlingStatus.Ignored && handleStatus == 0;
    }

    /// <summary>
    /// Returns true if any of the handlers reported a failure
    /// </summary>
    /// <param name="context">Consume context</param>
    /// <returns></returns>
    public static bool HasFailed(this IBaseConsumeContext context)
        => context.HandlingResults.GetFailureStatus() == EventHandlingStatus.Failure;
}