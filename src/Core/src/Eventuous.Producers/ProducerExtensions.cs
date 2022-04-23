namespace Eventuous.Producers;

[PublicAPI]
public static class ProducerExtensions {
    /// <summary>
    /// Produce a message of type <see cref="TMessage"/>. The type is used to look up the type name
    /// in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="producer"></param>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="message">Message to produce</param>
    /// <param name="metadata"></param>
    /// <param name="onAck">Function to confirm that the message was produced</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage">Message typ</typeparam>
    /// <returns></returns>
    public static Task Produce<TMessage>(
        this IEventProducer producer,
        StreamName          stream,
        TMessage            message,
        Metadata?           metadata,
        Func<ValueTask>?    onAck             = null,
        CancellationToken   cancellationToken = default
    ) where TMessage : class {
        var producedMessages =
            message is IEnumerable<object> collection
                ? ConvertMany(collection, metadata)
                : ConvertOne(message, metadata);

        return producer.Produce(stream, producedMessages, cancellationToken);
    }

    /// <summary>
    /// Produce a message of type <see cref="TMessage"/>. The type is used to look up the type name
    /// in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="producer">Producer instance</param>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="message">Message to produce</param>
    /// <param name="metadata">Message metadata</param>
    /// <param name="options">Produce options</param>
    /// <param name="onAck">Function to confirm that the message was produced</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <typeparam name="TProduceOptions"></typeparam>
    /// <returns></returns>
    public static Task Produce<TProduceOptions, TMessage>(
        this IEventProducer<TProduceOptions> producer,
        StreamName                           stream,
        TMessage                             message,
        Metadata?                            metadata,
        TProduceOptions                      options,
        Func<ValueTask>?                     onAck             = null,
        CancellationToken                    cancellationToken = default
    ) where TMessage : class where TProduceOptions : class {
        var producedMessages =
            Ensure.NotNull(message) is IEnumerable<object> collection
                ? ConvertMany(collection, metadata, onAck)
                : ConvertOne(message, metadata, onAck);

        return producer.Produce(stream, producedMessages, options, cancellationToken);
    }

    static IEnumerable<ProducedMessage> ConvertMany(
        IEnumerable<object> messages,
        Metadata?           metadata,
        Func<ValueTask>?    onAck = null
    )
        => messages.Select(x => new ProducedMessage(x, metadata) { OnAck = onAck });

    static IEnumerable<ProducedMessage> ConvertOne(object message, Metadata? metadata, Func<ValueTask>? onAck = null)
        => new[] { new ProducedMessage(message, metadata) { OnAck = onAck } };
}
