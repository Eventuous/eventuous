// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Microsoft.Extensions.Options;

// ReSharper disable InvertIf

namespace Eventuous.GooglePubSub.Producers;

/// <summary>
/// Producer for Google PubSub
/// </summary>
[PublicAPI]
public class GooglePubSubProducer : BaseProducer<PubSubProduceOptions>, IHostedProducer {
    readonly IEventSerializer _serializer;
    readonly ClientCache      _clientCache;
    readonly PubSubAttributes _attributes;

    /// <summary>
    /// Create a new instance of a Google PubSub producer
    /// </summary>
    /// <param name="projectId">GCP project ID</param>
    /// <param name="serializer">Optional event serializer. Will use the default instance if missing.</param>
    /// <param name="log">Optional logger instance</param>
    /// <param name="configureClient">Publisher client configuration action</param>
    public GooglePubSubProducer(
            string                          projectId,
            IEventSerializer?               serializer      = null,
            ILogger<GooglePubSubProducer>?  log             = null,
            Action<PublisherClientBuilder>? configureClient = null
        )
        : this(new PubSubProducerOptions { ProjectId = Ensure.NotEmptyString(projectId), ConfigureClientBuilder = configureClient }, serializer, log) { }

    /// <summary>
    /// Create a new instance of a Google PubSub producer
    /// </summary>
    /// <param name="options">Producer options</param>
    /// <param name="serializer">Optional event serializer. Will use the default instance if missing.</param>
    /// <param name="log">Optional logger instance</param>
    public GooglePubSubProducer(PubSubProducerOptions options, IEventSerializer? serializer = null, ILogger<GooglePubSubProducer>? log = null) : base(TracingOptions) {
        Ensure.NotNull(options);

        _serializer  = serializer ?? DefaultEventSerializer.Instance;
        _clientCache = new(options, log);
        _attributes  = options.Attributes;
        _log         = log;
    }

    /// <summary>
    /// Create a new instance of a Google PubSub producer
    /// </summary>
    /// <param name="options">Producer options</param>
    /// <param name="serializer">Optional event serializer. Will use the default instance if missing.</param>
    /// <param name="log">Optional logger instance</param>
    public GooglePubSubProducer(IOptions<PubSubProducerOptions> options, IEventSerializer? serializer = null, ILogger<GooglePubSubProducer>? log = null)
        : this(options.Value, serializer, log) { }

    public Task StartAsync(CancellationToken cancellationToken = default) {
        Ready = true;

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default) {
        _log?.LogInformation("Stopping Google PubSub clients...");
        await Task.WhenAll(_clientCache.GetAllClients().Select(x => x.ShutdownAsync(cancellationToken))).NoContext();
    }

    static readonly ProducerTracingOptions TracingOptions = new() { MessagingSystem = "google-pubsub", DestinationKind = "topc", ProduceOperation = "publish" };

    readonly ILogger<GooglePubSubProducer>? _log;

    protected override async Task ProduceMessages(
            StreamName                   stream,
            IEnumerable<ProducedMessage> messages,
            PubSubProduceOptions?        options,
            CancellationToken            cancellationToken = default
        ) {
        var client = await _clientCache.GetOrAddPublisher(stream, cancellationToken).NoContext();

        await Task.WhenAll(messages.Select(ProduceLocal)).NoContext();

        return;

        async Task ProduceLocal(ProducedMessage x) {
            try {
                await client.PublishAsync(CreateMessage(x, options)).NoContext();
                await x.Ack<GooglePubSubProducer>().NoContext();
            } catch (Exception e) {
                _log?.LogError(e, "Failed to produce to Google PubSub");
                await x.Nack<GooglePubSubProducer>("Failed to produce to Google PubSub", e).NoContext();
            }
        }
    }

    PubsubMessage CreateMessage(ProducedMessage message, PubSubProduceOptions? options) {
        var (messageType, contentType, payload) = _serializer.SerializeEvent(message.Message);
        SetActivityMessageType(messageType);

        var psm = new PubsubMessage {
            Data        = ByteString.CopyFrom(payload),
            OrderingKey = options?.OrderingKey ?? "",
            Attributes = {
                { _attributes.ContentType, contentType },
                { _attributes.EventType, messageType },
                { _attributes.MessageId, message.MessageId.ToString() }
            }
        };

        if (message.Metadata != null) {
            message.Metadata.Remove(MetaTags.MessageId);

            foreach (var (key, value) in message.Metadata) {
                if (value != null) psm.Attributes.Add(key, value.ToString());
            }
        }

        var attrs = options?.AddAttributes?.Invoke(message);

        if (attrs != null) {
            foreach (var (key, value) in attrs) { psm.Attributes.Add(key, value); }
        }

        return psm;
    }

    public bool Ready { get; private set; }
}
