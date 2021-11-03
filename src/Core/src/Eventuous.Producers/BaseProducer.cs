using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Producers.Diagnostics;

namespace Eventuous.Producers;

public abstract class BaseProducer<TProduceOptions> : BaseProducer, IEventProducer<TProduceOptions>
    where TProduceOptions : class {
    protected abstract Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        TProduceOptions?             options,
        CancellationToken            cancellationToken = default
    );

    /// <inheritdoc />
    public async Task Produce(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        TProduceOptions?             options,
        CancellationToken            cancellationToken = default
    ) {
        var (activity, msgs) = ProducerActivity.Start(messages, DefaultTags);

        if (activity is { IsAllDataRequested: true }) {
            activity.SetTag(TelemetryTags.Messaging.Destination, stream);
        }

        await ProduceMessages(stream, msgs, options, cancellationToken);

        activity?.Dispose();
    }

    /// <inheritdoc />
    protected override Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    )
        => ProduceMessages(stream, messages, null, cancellationToken);

    protected BaseProducer(ProducerTracingOptions? tracingOptions = null) : base(tracingOptions) { }
}

public abstract class BaseProducer : IEventProducer {
    protected KeyValuePair<string, object?>[] DefaultTags { get; }

    protected BaseProducer(ProducerTracingOptions? tracingOptions) {
        var options = tracingOptions ?? new ProducerTracingOptions();

        DefaultTags = new[] {
            new KeyValuePair<string, object?>(TelemetryTags.Messaging.System, options.MessagingSystem),
            new KeyValuePair<string, object?>(TelemetryTags.Messaging.DestinationKind, options.DestinationKind),
            new KeyValuePair<string, object?>(TelemetryTags.Messaging.Operation, options.ProduceOperation)
        };
    }

    protected abstract Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    );

    public async Task Produce(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        CancellationToken            cancellationToken = default
    ) {
        var messagesArray = messages.ToArray();
        if (messagesArray.Length == 0) return;

        var (activity, msgs) = messagesArray.Length == 1
            ? ForOne()
            : ProducerActivity.Start(messagesArray, DefaultTags);

        if (activity is { IsAllDataRequested: true }) {
            activity.SetTag(TelemetryTags.Messaging.Destination, stream);
        }

        await ProduceMessages(stream, msgs, cancellationToken);

        activity?.Dispose();

        (Activity? act, ProducedMessage[]) ForOne() {
            var (act, producedMessage) = ProducerActivity.Start(messagesArray[0], DefaultTags);
            return (act, new[] { producedMessage });
        }
    }

    public bool Ready { get; private set; }

    protected void ReadyNow() => Ready = true;
}