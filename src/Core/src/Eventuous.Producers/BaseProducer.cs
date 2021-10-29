using System.Diagnostics;
using Eventuous.Producers.Diagnostics;

namespace Eventuous.Producers;

public abstract class BaseProducer<TProduceOptions> : IEventProducer<TProduceOptions> where TProduceOptions : class {
    protected abstract Task ProduceMessages(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        TProduceOptions?             options,
        CancellationToken            cancellationToken = default
    );

    /// <inheritdoc />
    public Task Produce(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        TProduceOptions?             options,
        CancellationToken            cancellationToken = default
    ) => ProduceMessages(stream, messages, options, cancellationToken);

    /// <inheritdoc />
    public Task Produce(
        string                       stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    )
        => Produce(stream, messages, null, cancellationToken);

    protected void Trace(
        Action<ProducedMessage>                     produce,
        ProducedMessage                             message,
        IEnumerable<KeyValuePair<string, object?>>? tags,
        Action<Activity>?                           addInfraTags
    ) {
        var (activity, msg) = ProducerActivity.Start(message, tags, addInfraTags);
        produce(msg);
        activity?.Dispose();
    }

    protected async Task Trace(
        Func<ProducedMessage, Task>                 produceTask,
        ProducedMessage                             message,
        IEnumerable<KeyValuePair<string, object?>>? tags,
        Action<Activity>?                           addInfraTags
    ) {
        var (activity, msg) = ProducerActivity.Start(message, tags, addInfraTags);
        await produceTask(msg);
        activity?.Dispose();
    }

    protected async Task Trace(
        Func<IEnumerable<ProducedMessage>, Task> produceTask,
        IEnumerable<ProducedMessage>                        messages,
        IEnumerable<KeyValuePair<string, object?>>?         tags,
        Action<Activity>?                                   addInfraTags
    ) {
        var (activity, msgs) = ProducerActivity.Start(messages, tags, addInfraTags);
        await produceTask(msgs);
        activity?.Dispose();
    }

    public bool Ready { get; private set; }

    protected void ReadyNow() => Ready = true;
}