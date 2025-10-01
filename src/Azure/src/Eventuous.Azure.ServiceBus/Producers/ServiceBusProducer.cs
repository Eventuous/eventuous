// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;

namespace Eventuous.Azure.ServiceBus.Producers;

/// <summary>
/// Represents a producer for sending messages to Azure Service Bus.
/// </summary>
public class ServiceBusProducer : BaseProducer<ServiceBusProduceOptions>, IHostedProducer, IAsyncDisposable {
    // maybe want something a bit more focused on Azure Service Bus?
    static readonly ProducerTracingOptions TracingOptions = new() {
        MessagingSystem = "azure-service-bus", 
        DestinationKind = "topic", 
        ProduceOperation = "publish"
    };

    readonly ServiceBusProducerOptions     _options;
    readonly ILogger<ServiceBusProducer>?  _log;
    readonly ServiceBusSender              _sender;
    readonly IEventSerializer              _serializer;
    readonly ServiceBusMessageBatchBuilder _messageBatchBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceBusProducer"/> class.
    /// This constructor sets up the Service Bus sender and prepares it for sending messages.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="options"></param>
    /// <param name="serializer"></param>
    /// <param name="log"></param>
    public ServiceBusProducer(
            ServiceBusClient             client,
            ServiceBusProducerOptions    options,
            IEventSerializer?            serializer = null,
            ILogger<ServiceBusProducer>? log        = null
        ) : base(TracingOptions) {
        _options             = options;
        _log                 = log;
        _sender              = client.CreateSender(options.QueueOrTopicName, options.SenderOptions);
        _serializer          = serializer ?? DefaultEventSerializer.Instance;
        _messageBatchBuilder = new(_sender, this._serializer, options.AttributeNames, SetActivityMessageType);
        log?.LogInformation("ServiceBusProducer created for {QueueOrTopicName}", options.QueueOrTopicName);
    }

    /// <summary>
    /// Checks if the producer is ready to send messages.
    /// </summary>
    public bool Ready { get; private set; }

    /// <summary>
    /// Disposes the Service Bus sender and releases resources.
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync() {
        await _sender.DisposeAsync().NoContext();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Starts the producer and prepares it for sending messages.
    /// The sender is actually created in the constructor, so this method is primarily for logging and readiness.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task StartAsync(CancellationToken cancellationToken) {
        Ready = true;
        _log?.LogInformation("ServiceBusProducer started for {QueueOrTopicName}", _options.QueueOrTopicName);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the producer and releases resources.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StopAsync(CancellationToken cancellationToken) {
        Ready = false;
        await _sender.CloseAsync(cancellationToken).NoContext();
        _log?.LogInformation("ServiceBusProducer stopped for {QueueOrTopicName}", _options.QueueOrTopicName);
    }

    /// <summary>
    /// Actually sends the messages to the Service Bus queue or topic.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="messages"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override async Task ProduceMessages(StreamName stream, IEnumerable<ProducedMessage> messages, ServiceBusProduceOptions? options, CancellationToken cancellationToken = default) {
        if (messages is ProducedMessage[] { Length: 1 } singleMessage) {
            await ProcessSingleMessage(stream, options, singleMessage, cancellationToken);
        }
        else {
            await ProcessMessagesInBatches(stream, messages, options, cancellationToken);
        }
    }

    async Task ProcessSingleMessage(StreamName stream, ServiceBusProduceOptions? options, ProducedMessage[] singleMessage, CancellationToken cancellationToken) {
        var message = singleMessage[0];

        var serviceBusMessage = new ServiceBusMessageBuilder(
            _serializer,
            stream,
            _options.AttributeNames,
            options,
            SetActivityMessageType
        ).CreateServiceBusMessage(message);

        _log?.LogInformation("Sending single message to {QueueOrTopicName}", _options.QueueOrTopicName);

        try {
            await _sender.SendMessageAsync(serviceBusMessage, cancellationToken).NoContext();
            await message.Ack<ServiceBusProducer>().NoContext();
            _log?.LogInformation("Single message sent successfully to {QueueOrTopicName}", _options.QueueOrTopicName);
        } catch (Exception ex) {
            _log?.LogError(ex, "Failed to send single message to {QueueOrTopicName}", _options.QueueOrTopicName);
            await message.Nack<ServiceBusProducer>("Failed to send single message", ex).NoContext();
        }
    }

    async Task ProcessMessagesInBatches(StreamName stream, IEnumerable<ProducedMessage> messages, ServiceBusProduceOptions? options, CancellationToken cancellationToken) {
        await foreach (var (batch, produced) in _messageBatchBuilder.CreateMessageBatches(messages, stream, options, cancellationToken).NoContext(cancellationToken)) {
            _log?.LogInformation("Sending batch of {MessageCount} messages to {QueueOrTopicName}", batch.Count, _options.QueueOrTopicName);

            try {
                await _sender.SendMessagesAsync(batch, cancellationToken).NoContext();

                await produced.Select(x => x.Ack<ServiceBusProducer>()).WhenAll().NoContext();

                _log?.LogInformation("Batch sent successfully to {QueueOrTopicName}", _options.QueueOrTopicName);
            } catch (Exception ex) {
                _log?.LogError(ex, "Failed to send batch to {QueueOrTopicName}", _options.QueueOrTopicName);

                await produced.Select(x => x.Nack<ServiceBusProducer>("Failed to send batch", ex)).WhenAll().NoContext();
            }
        }
    }
}
