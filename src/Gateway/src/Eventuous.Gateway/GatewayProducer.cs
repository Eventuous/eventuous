using Eventuous.Diagnostics;

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
        await WaitForInner(_inner, cancellationToken);
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
        await WaitForInner(_inner, cancellationToken);
        await _inner.Produce(stream, messages, cancellationToken);
    }

    protected static async ValueTask WaitForInner(IEventProducer inner, CancellationToken cancellationToken) {
        if (inner is not IHostedProducer hosted) return;

        while (!hosted.Ready) {
            EventuousEventSource.Log.Warn("Producer not ready, waiting...");
            await Task.Delay(1000, cancellationToken);
        }
    }
}
