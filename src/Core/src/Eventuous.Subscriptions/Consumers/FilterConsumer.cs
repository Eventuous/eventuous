using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public delegate bool FilterMessage(IMessageConsumeContext receivedEvent);

public class FilterConsumer : IMessageConsumer {
    readonly IMessageConsumer _inner;
    readonly Type             _innerType;
    readonly FilterMessage    _filter;

    public FilterConsumer(IMessageConsumer inner, FilterMessage filter) {
        _inner     = Ensure.NotNull(inner, nameof(inner));
        _filter    = Ensure.NotNull(filter, nameof(filter));
        _innerType = _inner.GetType();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask Consume(IMessageConsumeContext context, CancellationToken cancellationToken) {
        if (_filter(context)) return _inner.Consume(context, cancellationToken);

        context.Ignore(_innerType);
        return default;
    }
}