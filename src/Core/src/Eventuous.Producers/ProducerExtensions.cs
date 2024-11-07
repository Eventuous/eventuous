// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Producers;

[PublicAPI]
public static class ProducerExtensions {
    /// <summary>
    /// Produce a message of type TMessage. The type is used to look up the type name
    /// in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="producer"></param>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="message">Message to produce</param>
    /// <param name="metadata"></param>
    /// <param name="additionalHeaders">Optional items to be used by the producer</param>
    /// <param name="onAck">Function to confirm that the message was produced</param>
    /// <param name="onNack">Function to report that the message wasn't produced successfully</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage">Message typ</typeparam>
    /// <returns></returns>
    public static Task Produce<TMessage>(
            this IProducer       producer,
            StreamName           stream,
            TMessage             message,
            Metadata?            metadata,
            Metadata?            additionalHeaders = null,
            AcknowledgeProduce?  onAck             = null,
            ReportFailedProduce? onNack            = null,
            CancellationToken    cancellationToken = default
        ) where TMessage : class {
        var producedMessages =
            Ensure.NotNull(message) is IEnumerable<object> collection
                ? ConvertMany(collection, metadata, additionalHeaders, onAck, onNack)
                : ConvertOne(message, metadata, additionalHeaders, onAck, onNack);

        return producer.Produce(stream, producedMessages, cancellationToken);
    }

    /// <summary>
    /// Produce a message of type <code>TMessage</code>. The type is used to look up the type name
    /// in the <seealso cref="TypeMap"/>.
    /// </summary>
    /// <param name="producer">Producer instance</param>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="message">Message to produce</param>
    /// <param name="metadata">Message metadata</param>
    /// <param name="options">Produce options</param>
    /// <param name="additionalHeaders">Optional items to be used by the producer</param>
    /// <param name="onAck">Function to confirm that the message was produced</param>
    /// <param name="onNack">Function to report that the message wasn't produced successfully</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <typeparam name="TProduceOptions"></typeparam>
    /// <returns></returns>
    public static Task Produce<TProduceOptions, TMessage>(
            this IProducer<TProduceOptions> producer,
            StreamName                      stream,
            TMessage                        message,
            Metadata?                       metadata,
            TProduceOptions?                options           = null,
            Metadata?                       additionalHeaders = null,
            AcknowledgeProduce?             onAck             = null,
            ReportFailedProduce?            onNack            = null,
            CancellationToken               cancellationToken = default
        ) where TMessage : class where TProduceOptions : class {
        var producedMessages = Ensure.NotNull(message) is IEnumerable<object> collection
            ? ConvertMany(collection, metadata, additionalHeaders, onAck, onNack)
            : ConvertOne(message, metadata, additionalHeaders, onAck, onNack);

        return producer.Produce(stream, producedMessages, options, cancellationToken);
    }

    static IEnumerable<ProducedMessage> ConvertMany(
            IEnumerable<object>  messages,
            Metadata?            metadata,
            Metadata?            additionalHeaders,
            AcknowledgeProduce?  onAck,
            ReportFailedProduce? onNack
        )
        => messages.Select(x => new ProducedMessage(x, metadata, additionalHeaders) { OnAck = onAck, OnNack = onNack });

    static ProducedMessage[] ConvertOne(
            object               message,
            Metadata?            metadata,
            Metadata?            additionalHeaders,
            AcknowledgeProduce?  onAck,
            ReportFailedProduce? onNack
        )
        => [new ProducedMessage(message, metadata, additionalHeaders) { OnAck = onAck, OnNack = onNack }];
}
