namespace Eventuous.Producers; 

public abstract class BaseProducer<TProduceOptions> : IEventProducer<TProduceOptions> where TProduceOptions : class {
    public abstract Task ProduceMessage(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        TProduceOptions?             options,
        CancellationToken            cancellationToken = default
    );

    public Task ProduceMessages(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    )
        => ProduceMessage(stream, messages, null, cancellationToken);

    public bool Ready { get; private set; }

    protected void ReadyNow() => Ready = true;
}