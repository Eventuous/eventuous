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
    ServiceBusSessionProcessor?                _sessionProcessor;

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
        if (Options.SessionProcessorOptions is not null) {
            _sessionProcessor = Options.QueueOrTopic.MakeSessionProcessor(_client, Options);

            _sessionProcessor.ProcessMessageAsync += HandleSessionMessage;
            _sessionProcessor.ProcessErrorAsync   += _defaultErrorHandler;

            return new(_sessionProcessor.StartProcessingAsync(cancellationToken));
        }

        _processor = Options.QueueOrTopic.MakeProcessor(_client, Options);

        _processor.ProcessMessageAsync += HandleMessage;
        _processor.ProcessErrorAsync   += _defaultErrorHandler;

        return new(_processor.StartProcessingAsync(cancellationToken));

        Task HandleMessage(ProcessMessageEventArgs arg)
            => ProcessMessageAsync(
                arg.Message,
                arg.CancellationToken,
                msg => arg.CompleteMessageAsync(msg, arg.CancellationToken),
                msg => arg.AbandonMessageAsync(msg, null, arg.CancellationToken),
                arg.FullyQualifiedNamespace,
                arg.EntityPath,
                arg.Identifier
            );

        Task HandleSessionMessage(ProcessSessionMessageEventArgs arg)
            => ProcessMessageAsync(
                arg.Message,
                arg.CancellationToken,
                msg => arg.CompleteMessageAsync(msg, arg.CancellationToken),
                msg => arg.AbandonMessageAsync(msg, null, arg.CancellationToken),
                arg.FullyQualifiedNamespace,
                arg.EntityPath,
                arg.Identifier
            );

        async Task ProcessMessageAsync(
                ServiceBusReceivedMessage             msg,
                CancellationToken                     ct,
                Func<ServiceBusReceivedMessage, Task> completeMessage,
                Func<ServiceBusReceivedMessage, Task> abandonMessage,
                string                                fullyQualifiedNamespace,
                string                                entityPath,
                string                                identifier
            ) {
            if (ct.IsCancellationRequested) return;

            var eventType = (msg.ApplicationProperties.TryGetValue(Options.AttributeNames.MessageType, out var messageType)
                ? messageType.ToString()
                : msg.Subject) ?? throw new InvalidOperationException("Message type is missing in message properties");
            var contentType = msg.ContentType;

            // Should this be a stream name? or topic or something
            var streamName = (msg.ApplicationProperties.TryGetValue(Options.AttributeNames.StreamName, out var stream)
                ? stream.ToString()
                : Options.QueueOrTopic switch {
                    Queue queue => queue.Name,
                    Topic topic => topic.Name,
                    _           => null
                }) ?? throw new InvalidOperationException("Stream name is missing in message properties");

            Logger.Current = Log;
            var evt                   = DeserializeData(contentType, eventType, msg.Body, streamName);
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
                await completeMessage(msg).NoContext();
            } catch (Exception ex) {
                // Abandoning the message will make it available for reprocessing, or dead letter it?
                await abandonMessage(msg).NoContext();
                await _defaultErrorHandler(new(ex, ServiceBusErrorSource.Abandon, fullyQualifiedNamespace, entityPath, identifier, ct)).NoContext();
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

    Task DefaultErrorHandler(ProcessErrorEventArgs arg) {
        Log.ErrorLog?.Log(arg.Exception, "Error processing message: {Identifier}", arg.Identifier);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Unsubscribes from the Service Bus queue or topic and stops processing messages.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        if (_sessionProcessor is not null) {
            await _sessionProcessor.StopProcessingAsync(cancellationToken).NoContext();
        }
        else if (_processor is not null) {
            await _processor.StopProcessingAsync(cancellationToken).NoContext();
        }
    }
}
