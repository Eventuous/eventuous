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
    /// <param name="additionalHeaders">Optional items to be used by the producer</param>
    /// <param name="onAck">Function to confirm that the message was produced</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage">Message typ</typeparam>
    /// <returns></returns>
    public static Task Produce<TMessage>(
        this IEventProducer producer,
        StreamName          stream,
        TMessage            message,
        Metadata?           metadata,
        Metadata?           additionalHeaders = null,
        AcknowledgeProduce? onAck             = null,
        CancellationToken   cancellationToken = default
    ) where TMessage : class {
        var producedMessages =
            message is IEnumerable<object> collection
                ? ConvertMany(collection, metadata, additionalHeaders, onAck)
                : ConvertOne(message, metadata, additionalHeaders, onAck);

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
    /// <param name="additionalHeaders">Optional items to be used by the producer</param>
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
        Metadata?                            additionalHeaders = null,
        AcknowledgeProduce?                  onAck             = null,
        CancellationToken                    cancellationToken = default
    ) where TMessage : class where TProduceOptions : class {
        var producedMessages =
            Ensure.NotNull(message) is IEnumerable<object> collection
                ? ConvertMany(collection, metadata, additionalHeaders, onAck)
                : ConvertOne(message, metadata, additionalHeaders, onAck);

        return producer.Produce(stream, producedMessages, options, cancellationToken);
    }

    static IEnumerable<ProducedMessage> ConvertMany(
        IEnumerable<object> messages,
        Metadata?           metadata,
        Metadata?           additionalHeaders,
        AcknowledgeProduce? onAck
    )
        => messages.Select(x => new ProducedMessage(x, metadata, additionalHeaders) { OnAck = onAck });

    static IEnumerable<ProducedMessage> ConvertOne(
        object              message,
        Metadata?           metadata,
        Metadata?           additionalHeaders,
        AcknowledgeProduce? onAck
    )
        => new[] { new ProducedMessage(message, metadata, additionalHeaders) { OnAck = onAck } };
}
