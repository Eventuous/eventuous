namespace Eventuous.Shovel;

class ShovelProducer<T> : BaseProducer<T> where T : class {
    readonly IEventProducer<T> _inner;

    public ShovelProducer(IEventProducer<T> inner) => _inner = inner;

    public override async Task ProduceMessages(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        T?                           options,
        CancellationToken            cancellationToken = default
    ) {
        while (!_inner.Ready) await Task.Delay(10, cancellationToken);

        await _inner.ProduceMessages(stream, messages, options, cancellationToken);
    }
}