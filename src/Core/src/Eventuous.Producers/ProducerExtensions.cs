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
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage">Message typ</typeparam>
    /// <returns></returns>
    public static Task Produce<TMessage>(
        this IEventProducer producer,
        string              stream,
        TMessage            message,
        CancellationToken   cancellationToken = default
    ) where TMessage : class {
        var producedMessages =
            message is IEnumerable<object> collection
                ? ConvertMany(collection)
                : ConvertOne(message);

        return producer.ProduceMessages(stream, producedMessages, cancellationToken);
    }

    /// <summary>
    /// Produce a batch of messages, use the message type returned by message.GetType,
    /// then look it up in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="producer"></param>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="messages">Messages to produce</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task Produce(
        this IEventProducer producer,
        string              stream,
        IEnumerable<object> messages,
        CancellationToken   cancellationToken = default
    )
        => producer.ProduceMessages(stream, ConvertMany(messages), cancellationToken);

    /// <summary>
    /// Produce a message of type <see cref="TMessage"/>. The type is used to look up the type name
    /// in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="producer">Producer instance</param>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="message">Message to produce</param>
    /// <param name="options">Produce options</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <typeparam name="TProduceOptions"></typeparam>
    /// <returns></returns>
    public static Task Produce<TProduceOptions, TMessage>(
        this IEventProducer<TProduceOptions> producer,
        string                               stream,
        TMessage                             message,
        TProduceOptions?                     options,
        CancellationToken                    cancellationToken = default
    )
        where TMessage : class where TProduceOptions : class {
        var producedMessages =
            message is IEnumerable<object> collection
                ? ConvertMany(collection)
                : ConvertOne(message);

        return producer.ProduceMessages(stream, producedMessages, options, cancellationToken);
    }

    /// <summary>
    /// Produce a batch of messages, use the message type returned by message.GetType,
    /// then look it up in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="producer">Producer instance</param>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="messages">Messages to produce</param>
    /// <param name="options">Produce options</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task Produce<TProduceOptions>(
        this IEventProducer<TProduceOptions> producer,
        string                               stream,
        IEnumerable<object>                  messages,
        TProduceOptions?                     options,
        CancellationToken                    cancellationToken = default
    ) where TProduceOptions : class
        => producer.ProduceMessages(stream, ConvertMany(messages), options, cancellationToken);

    static IEnumerable<ProducedMessage> ConvertMany(IEnumerable<object> messages)
        => messages.Select(x => new ProducedMessage(x, null));

    static IEnumerable<ProducedMessage> ConvertOne(object message)
        => new[] { new ProducedMessage(message, null) };
}