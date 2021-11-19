using System.Diagnostics;
using System.Runtime.CompilerServices;
using Eventuous.Diagnostics;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;
using ActivityStatus = Eventuous.Diagnostics.ActivityStatus;

namespace Eventuous.Subscriptions.Context;

public static class ContextExtensions {
    public static void Ack(this IBaseConsumeContext context, string handlerType) {
        context.HandlingResults.Add(EventHandlingResult.Succeeded(handlerType));
        Log.MessageHandled(handlerType, context);
    }

    public static void Nack(
        this IBaseConsumeContext context,
        string                   handlerType,
        Exception?               exception
    ) {
        context.HandlingResults.Add(EventHandlingResult.Failed(handlerType, exception));
        Log.MessageHandlingFailed(handlerType, context, exception);

        if (Activity.Current != null && Activity.Current.Status != ActivityStatusCode.Error) {
            Activity.Current.SetActivityStatus(
                ActivityStatus.Error(exception, $"Error handling {context.MessageType}")
            );
        }
    }

    public static void Ignore(this IBaseConsumeContext context, string handlerType) {
        context.HandlingResults.Add(EventHandlingResult.Ignored(handlerType));
        Log.MessageIgnored(handlerType, context);
    }

    public static void Ack<T>(this IBaseConsumeContext context) => context.Ack(typeof(T).Name);

    public static void Ignore<T>(this IBaseConsumeContext context) => context.Ignore(typeof(T).Name);

    public static void Nack<T>(this IBaseConsumeContext context, Exception? exception)
        => context.Nack(typeof(T).Name, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WasIgnored(this IBaseConsumeContext context) {
        var status       = context.HandlingResults.GetIgnoreStatus();
        var handleStatus = context.HandlingResults.GetFailureStatus();

        return (status & EventHandlingStatus.Ignored) == EventHandlingStatus.Ignored
            && handleStatus == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StatusUpdatedBy(this IBaseConsumeContext context, string handlerType)
        => context.HandlingResults.ReportedBy(handlerType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WasIgnoredBy(this IBaseConsumeContext context, string handlerType)
        => context.HandlingResults.GetResultsOf(EventHandlingStatus.Ignored).Any(x => x.HandlerType == handlerType);

    public static bool HasFailed(this IBaseConsumeContext context)
        => context.HandlingResults.GetFailureStatus() == EventHandlingStatus.Failure;
}