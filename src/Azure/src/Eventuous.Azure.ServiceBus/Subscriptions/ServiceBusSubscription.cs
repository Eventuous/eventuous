// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Azure.ServiceBus.Subscriptions;

/// <summary>
/// Represents a Service Bus subscription that processes messages from a queue or topic.
/// </summary>
public class ServiceBusSubscription : EventSubscription<ServiceBusSubscriptionOptions> {
    readonly ServiceBusClient                  _client;
    readonly Func<ProcessErrorEventArgs, Task> _defaultErrorHandler;
    ServiceBusProcessor?                       _processor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceBusSubscription"/> class.
    /// </summary>
    /// <param name="client">Service Bus client</param>
    /// <param name="options">Service Bus subscription options</param>
    /// <param name="consumePipe">Consume pipe instance</param>
    /// <param name="loggerFactory">Logger factory (optional)</param>
    /// <param name="eventSerializer">Event serializer (optional)</param>
    public ServiceBusSubscription(ServiceBusClient client, ServiceBusSubscriptionOptions options, ConsumePipe consumePipe, ILoggerFactory? loggerFactory, IEventSerializer? eventSerializer) :
        base(options, consumePipe, loggerFactory, eventSerializer) {
        _client              = client;
        _defaultErrorHandler = Options.ErrorHandler ?? DefaultErrorHandler;
    }

    /// <summary>
    /// Subscribes to the Service Bus queue or topic.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override ValueTask Subscribe(CancellationToken cancellationToken) {
        _processor = Options.QueueOrTopic.MakeProcessor(_client, Options);

        _processor.ProcessMessageAsync += HandleMessage;
        _processor.ProcessErrorAsync   += _defaultErrorHandler;

        return new(_processor.StartProcessingAsync(cancellationToken));

        async Task HandleMessage(ProcessMessageEventArgs arg) {
            var ct = arg.CancellationToken;

            if (ct.IsCancellationRequested) return;

            var msg = arg.Message;

            var eventType = msg.ApplicationProperties[Options.AttributeNames.MessageType].ToString()
             ?? throw new InvalidOperationException("Event type is missing in message properties");
            var contentType = msg.ContentType;

            // Should this be a stream name? or topic or something
            var streamName = msg.ApplicationProperties[Options.AttributeNames.StreamName].ToString()
             ?? throw new InvalidOperationException("Stream name is missing in message properties");

            Logger.Current = Log;

            var evt = DeserializeData(contentType, eventType, msg.Body, streamName);

            var applicationProperties = msg.ApplicationProperties.Concat(MessageProperties(msg));

            var ctx = new MessageConsumeContext(
                msg.MessageId,
                eventType,
                contentType,
                streamName,
                0,
                0,
                0,
                Sequence++,
                msg.EnqueuedTime.UtcDateTime,
                evt,
                AsMeta(applicationProperties),
                SubscriptionId,
                ct
            );

            try {
                await Handler(ctx).NoContext();
                await arg.CompleteMessageAsync(msg, ct).NoContext();
            } catch (Exception ex) {
                // Abandoning the message will make it available for reprocessing, or dead letter it?
                await arg.AbandonMessageAsync(msg, null, ct).NoContext(); 
                await _defaultErrorHandler(new(ex, ServiceBusErrorSource.Abandon, arg.FullyQualifiedNamespace, arg.EntityPath, arg.Identifier, arg.CancellationToken)).NoContext();
                Log.ErrorLog?.Log(ex, "Error processing message: {MessageId}", msg.MessageId);
            }
        }
    }

    IEnumerable<KeyValuePair<string, object>> MessageProperties(ServiceBusReceivedMessage msg) {
        var attributes = Options.AttributeNames;

        if (msg.CorrelationId is not null)
            yield return new(attributes.CorrelationId, msg.CorrelationId);

        if (msg.ReplyTo is not null)
            yield return new(attributes.ReplyTo, msg.ReplyTo);

        if (msg.Subject is not null)
            yield return new(attributes.Subject, msg.Subject);

        if (msg.To is not null)
            yield return new(attributes.To, msg.To);

        if (msg.MessageId is not null)
            yield return new(attributes.MessageId, msg.MessageId);
    }

    static Metadata AsMeta(IEnumerable<KeyValuePair<string, object>> applicationProperties) =>
        new(applicationProperties.ToDictionary(pair => pair.Key, object? (pair) => pair.Value));

    async Task DefaultErrorHandler(ProcessErrorEventArgs arg) {
        // Log the error
        Log.ErrorLog?.Log(arg.Exception, "Error processing message: {Identifier}", arg.Identifier);

        // Optionally, you can handle the error further, e.g., by sending to a dead-letter queue
        await Task.CompletedTask;
    }

    /// <summary>
    /// Unsubscribes from the Service Bus queue or topic and stops processing messages.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        if (_processor == null) return;
        await _processor.StopProcessingAsync(cancellationToken).NoContext();
    }
}
