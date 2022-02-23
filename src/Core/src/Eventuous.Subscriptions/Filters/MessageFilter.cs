using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public delegate bool FilterMessage(IMessageConsumeContext receivedEvent);

public class MessageFilter : ConsumeFilter {
    readonly FilterMessage _filter;

    public MessageFilter(FilterMessage filter) => _filter = Ensure.NotNull(filter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask Send(IMessageConsumeContext context, Func<IMessageConsumeContext, ValueTask>? next) {
        if (next == null) return default;

        if (_filter(context)) return next(context);

        context.Ignore<MessageFilter>();
        return default;
    }
}