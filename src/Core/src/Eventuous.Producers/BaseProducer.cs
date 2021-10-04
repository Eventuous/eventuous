namespace Eventuous.Producers; 

public abstract class BaseProducer<TProduceOptions> : IEventProducer<TProduceOptions> where TProduceOptions : class {
    /// <inheritdoc />
    public abstract Task ProduceMessages(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        TProduceOptions?             options,
        CancellationToken            cancellationToken = default
    );

    /// <inheritdoc />
    public Task ProduceMessages(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    )
        => ProduceMessages(stream, messages, null, cancellationToken);

    public bool Ready { get; private set; }

    protected void ReadyNow() => Ready = true;
}