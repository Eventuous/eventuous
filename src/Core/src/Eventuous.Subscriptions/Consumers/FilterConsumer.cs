using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public delegate bool FilterMessage(IMessageConsumeContext receivedEvent);

public class FilterConsumer : MessageConsumer {
    readonly MessageConsumer _inner;
    readonly Type             _innerType;
    readonly FilterMessage    _filter;

    public FilterConsumer(MessageConsumer inner, FilterMessage filter) {
        _inner     = Ensure.NotNull(inner, nameof(inner));
        _filter    = Ensure.NotNull(filter, nameof(filter));
        _innerType = _inner.GetType();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask Consume(IMessageConsumeContext context) {
        if (_filter(context)) return _inner.Consume(context);

        context.Ignore(_innerType);
        return default;
    }
}