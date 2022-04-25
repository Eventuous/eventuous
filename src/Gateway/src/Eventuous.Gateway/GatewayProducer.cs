namespace Eventuous.Gateway;

public class GatewayProducer<T> : GatewayProducer, IEventProducer<T> where T : class {
    readonly IEventProducer<T> _inner;

    public GatewayProducer(IEventProducer<T> inner) : base(inner) => _inner = inner;

    public async Task Produce(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        T?                           options,
        CancellationToken            cancellationToken = default
    ) {
        while (!_inner.Ready) await Task.Delay(10, cancellationToken);

        await _inner.Produce(stream, messages, options, cancellationToken);
    }
}

public class GatewayProducer : IEventProducer {
    readonly IEventProducer _inner;

    public GatewayProducer(IEventProducer inner) => _inner = inner;

    public async Task Produce(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    ) {
        while (!_inner.Ready) await Task.Delay(10, cancellationToken);

        await _inner.Produce(stream, messages, cancellationToken);
    }

    public bool Ready => true;
}
