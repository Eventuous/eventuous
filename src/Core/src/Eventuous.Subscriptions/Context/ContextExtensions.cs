using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions.Context;

public static class ContextExtensions {
    public static void Ack(this IBaseConsumeContext context, Type? handlerType)
        => context.HandlingResults.Add(EventHandlingResult.Succeeded(handlerType));

    public static void Ignore(this IBaseConsumeContext context, Type? handlerType)
        => context.HandlingResults.Add(EventHandlingResult.Ignored(handlerType));

    public static void Nack(
        this IBaseConsumeContext context,
        Type?                    handlerType,
        Exception?               exception
    ) => context.HandlingResults.Add(EventHandlingResult.Failed(handlerType, exception));

    public static void Ack<T>(this IBaseConsumeContext context)
        => context.HandlingResults.Add(EventHandlingResult.Succeeded(typeof(T)));

    public static void Ignore<T>(this IBaseConsumeContext context)
        => context.HandlingResults.Add(EventHandlingResult.Ignored(typeof(T)));

    public static void Nack<T>(this IBaseConsumeContext context, Exception? exception)
        => context.HandlingResults.Add(EventHandlingResult.Failed(typeof(T), exception));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WasIgnored(this IBaseConsumeContext context) {
        var status       = context.HandlingResults.GetIgnoreStatus();
        var handleStatus = context.HandlingResults.GetFailureStatus();

        return (status & EventHandlingStatus.Ignored) == EventHandlingStatus.Ignored
            && handleStatus == 0;
    }

    public static bool HasFailed(this IBaseConsumeContext context)
        => context.HandlingResults.GetFailureStatus() == EventHandlingStatus.Failure;
}