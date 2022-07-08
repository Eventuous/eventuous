using Microsoft.Extensions.Hosting;

namespace Eventuous.Producers;

[PublicAPI]
public interface IEventProducer {
    /// <summary>
    /// Produce a message wrapped in the <see cref="ProducedMessage"/>.
    /// in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="messages">Collection of messages to produce</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task Produce(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    );
}

[PublicAPI]
public interface IEventProducer<in TProduceOptions> : IEventProducer where TProduceOptions : class {
    /// <summary>
    /// Produce a message wrapped in the <see cref="ProducedMessage"/>.
    /// in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="messages">Collection of messages to produce</param>
    /// <param name="options">Produce options</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task Produce(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        TProduceOptions?             options,
        CancellationToken            cancellationToken = default
    );
}

public interface IHostedProducer : IHostedService {
    bool Ready { get; }
}