using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public delegate bool FilterMessage(IMessageConsumeContext receivedEvent);

public class FilterConsumer : IMessageConsumer {
    readonly IMessageConsumer _inner;
    readonly FilterMessage    _filter;

    public FilterConsumer(IMessageConsumer inner, FilterMessage filter) {
        _inner  = Ensure.NotNull(inner, nameof(inner));
        _filter = Ensure.NotNull(filter, nameof(filter));
    }

    public ValueTask Consume(IMessageConsumeContext context, CancellationToken cancellationToken)
        => _filter(context) ? _inner.Consume(context, cancellationToken) : default;
}