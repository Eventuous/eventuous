// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Eventuous.Tools;

namespace Eventuous.EventStore.Producers;

/// <summary>
/// Producer for EventStoreDB
/// </summary>
[PublicAPI]
public class EventStoreProducer : BaseProducer<EventStoreProduceOptions> {
    readonly EventStoreClient    _client;
    readonly IEventSerializer    _serializer;
    readonly IMetadataSerializer _metaSerializer;

    /// <summary>
    /// Create a new EventStoreDB producer instance
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client</param>
    /// <param name="serializer">Optional: event serializer instance</param>
    /// <param name="metaSerializer">Optional: metadata serializer instance</param>
    public EventStoreProducer(EventStoreClient eventStoreClient, IEventSerializer? serializer = null, IMetadataSerializer? metaSerializer = null)
        : base(TracingOptions) {
        _client         = Ensure.NotNull(eventStoreClient);
        _serializer     = serializer     ?? DefaultEventSerializer.Instance;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
    }

    /// <summary>
    /// Create a new EventStoreDB producer instance
    /// </summary>
    /// <param name="clientSettings">EventStoreDB gRPC client settings</param>
    /// <param name="serializer">Optional: event serializer instance</param>
    /// <param name="metaSerializer">Optional: metadata serializer instance</param>
    public EventStoreProducer(EventStoreClientSettings clientSettings, IEventSerializer? serializer = null, IMetadataSerializer? metaSerializer = null)
        : this(new EventStoreClient(Ensure.NotNull(clientSettings)), serializer, metaSerializer) { }

    static readonly ProducerTracingOptions TracingOptions = new() {
        DestinationKind  = "stream",
        MessagingSystem  = "eventstoredb",
        ProduceOperation = "append"
    };

    /// <summary>
    /// Appends a batch of messages to a stream
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="messages">Batch of messages</param>
    /// <param name="produceOptions">Options for the produce operation</param>
    /// <param name="cancellationToken"></param>
    protected override async Task ProduceMessages(
            StreamName                   stream,
            IEnumerable<ProducedMessage> messages,
            EventStoreProduceOptions?    produceOptions,
            CancellationToken            cancellationToken = default
        ) {
        var options = produceOptions ?? EventStoreProduceOptions.Default;

        foreach (var chunk in Ensure.NotNull(messages).Chunks(options.MaxAppendEventsCount)) {
            var chunkMessages = chunk.ToArray();

            var setMessageType = chunkMessages.Length == 1;

            try {
                await _client.AppendToStreamAsync(
                        stream,
                        options.ExpectedState,
                        // ReSharper disable once ConvertClosureToMethodGroup
                        chunkMessages.Select(message => CreateMessage(message, setMessageType)),
                        null,
                        options.Deadline,
                        options.Credentials,
                        cancellationToken
                    )
                    .NoContext();

                await chunkMessages.Select(x => x.Ack<EventStoreProducer>()).WhenAll().NoContext();
            } catch (Exception e) {
                await chunkMessages
                    .Select(x => x.Nack<EventStoreProducer>("Unable to produce to EventStoreDB", e))
                    .WhenAll()
                    .NoContext();
            }
        }
    }

    EventData CreateMessage(ProducedMessage message, bool setMessageType) {
        var msg = Ensure.NotNull(message.Message);
        var (eventType, contentType, payload) = _serializer.SerializeEvent(msg);
        message.Metadata!.Remove(MetaTags.MessageId);

        if (setMessageType) {
            SetActivityMessageType(eventType);
        }
        var metaBytes = _metaSerializer.Serialize(message.Metadata);

        return new(Uuid.FromGuid(message.MessageId), eventType, payload, metaBytes, contentType);
    }
}
