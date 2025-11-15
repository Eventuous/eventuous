// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Eventuous.Diagnostics;

namespace Eventuous.Subscriptions.Context;

using Logging;

public static class ContextResultExtensions {
    /// <param name="context">Consume context</param>
    extension(IBaseConsumeContext context) {
        /// <summary>
        /// Allows acknowledging the message by a specific handler, identified by a string
        /// </summary>
        /// <param name="handlerType">Handler type identifier</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ack(string handlerType) {
            context.HandlingResults.Add(EventHandlingResult.Succeeded(handlerType));
            context.LogContext.MessageHandled(handlerType, context);
        }

        /// <summary>
        /// Allows conveying the message handling failure that occurred in a specific handler
        /// </summary>
        /// <param name="handlerType">Handler type identifier</param>
        /// <param name="exception">Optional: handler exception</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Nack(string handlerType, Exception? exception) {
            context.HandlingResults.Add(EventHandlingResult.Failed(handlerType, exception));

            context.LogContext.MessageHandlingFailed(handlerType, context, exception);

            if (Activity.Current != null && Activity.Current.Status != ActivityStatusCode.Error) {
                Activity.Current.SetActivityStatus(
                    ActivityStatus.Error(exception, $"Error handling {context.MessageType}")
                );
            }
        }

        /// <summary>
        /// Allows conveying the fact that the message was ignored by the handler
        /// </summary>
        /// <param name="handlerType">Handler type identifier</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ignore(string handlerType) {
            context.HandlingResults.Add(EventHandlingResult.Ignored(handlerType));
            context.LogContext.MessageIgnored(handlerType, context);
        }

        /// <summary>
        /// Allows acknowledging the message by a specific handler, identified by a string
        /// </summary>
        /// <typeparam name="T">Handler type</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ack<T>() => context.Ack(typeof(T).Name);

        /// <summary>
        /// Allows conveying the fact that the message was ignored by the handler
        /// </summary>
        /// <typeparam name="T">Handler type</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Ignore<T>() => context.Ignore(typeof(T).Name);

        /// <summary>
        /// Allows conveying the message handling failure that occurred in a specific handler
        /// </summary>
        /// <param name="exception">Optional: handler exception</param>
        /// <typeparam name="T">Handler type</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Nack<T>(Exception? exception) => context.Nack(typeof(T).Name, exception);

        /// <summary>
        /// Returns true if the message was ignored by all handlers
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WasIgnored() {
            var status       = context.HandlingResults.GetIgnoreStatus();
            var handleStatus = context.HandlingResults.GetFailureStatus();

            return (status & EventHandlingStatus.Ignored) == EventHandlingStatus.Ignored && handleStatus == 0;
        }

        /// <summary>
        /// Returns true if any of the handlers reported a failure
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFailed()
            => context.HandlingResults.GetFailureStatus() == EventHandlingStatus.Failure;
    }
}
