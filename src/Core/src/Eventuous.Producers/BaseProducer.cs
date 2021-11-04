using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Producers.Diagnostics;
using static Eventuous.Diagnostics.TelemetryTags;

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
            activity.SetTag(Messaging.Destination, stream);
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

    protected BaseProducer(ProducerTracingOptions? tracingOptions = null) {
        var options = tracingOptions ?? new ProducerTracingOptions();

        DefaultTags = new[] {
            new KeyValuePair<string, object?>(Messaging.System, options.MessagingSystem),
            new KeyValuePair<string, object?>(Messaging.DestinationKind, options.DestinationKind),
            new KeyValuePair<string, object?>(Messaging.Operation, options.ProduceOperation)
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
            activity.SetTag(Messaging.Destination, stream);
            activity.SetTag(TelemetryTags.Eventuous.Stream, stream);
        }

        await ProduceMessages(stream, msgs, cancellationToken);

        activity?.Dispose();

        (Activity? act, ProducedMessage[]) ForOne() {
            var (act, producedMessage) = ProducerActivity.Start(messagesArray[0], DefaultTags);

            if (act is { IsAllDataRequested: true }) {
                var messageId = producedMessage.MessageId.ToString();
                act.SetTag(Message.Type, producedMessage.MessageType);
                act.SetTag(Message.Id, messageId);
                act.SetTag(Messaging.MessageId, messageId);
            }

            return (act, new[] { producedMessage });
        }
    }

    public bool Ready { get; private set; }

    protected void ReadyNow() => Ready = true;
}