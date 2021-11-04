namespace Eventuous.Shovel;

class ShovelProducer<T> : BaseProducer<T> where T : class {
    readonly IEventProducer<T> _inner;

    public ShovelProducer(IEventProducer<T> inner) => _inner = inner;

    protected override async Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        T?                           options,
        CancellationToken            cancellationToken = default
    ) {
        while (!_inner.Ready) await Task.Delay(10, cancellationToken);

        await _inner.Produce(stream, messages, options, cancellationToken);
    }
}
class ShovelProducer : BaseProducer {
    readonly IEventProducer _inner;

    public ShovelProducer(IEventProducer inner) => _inner = inner;

    protected override async Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    ) {
        while (!_inner.Ready) await Task.Delay(10, cancellationToken);

        await _inner.Produce(stream, messages, cancellationToken);
    }
}
