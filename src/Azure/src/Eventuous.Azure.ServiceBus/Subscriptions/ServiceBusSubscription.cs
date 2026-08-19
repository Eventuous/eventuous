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
    readonly Func<ProcessErrorEventArgs, Task> _defaultErrorHandler;
    readonly IServiceBusProcessorStrategy      _processorStrategy;

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
        _defaultErrorHandler = Options.ErrorHandler ?? DefaultErrorHandler;

        _processorStrategy = Options.SessionProcessorOptions is not null
            ? new SessionProcessorStrategy(client, Options, HandleSessionMessage, _defaultErrorHandler)
            : new StandardProcessorStrategy(client, Options, HandleMessage, _defaultErrorHandler);
    }

    /// <summary>
    /// Subscribes to the Service Bus queue or topic.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override ValueTask Subscribe(CancellationToken cancellationToken)
        => _processorStrategy.Start(cancellationToken);

    Task HandleMessage(ProcessMessageEventArgs arg)
        => ProcessMessageAsync(
            arg.Message,
            msg => arg.CompleteMessageAsync(msg, arg.CancellationToken),
            msg => arg.AbandonMessageAsync(msg, null, arg.CancellationToken),
            arg.FullyQualifiedNamespace,
            arg.EntityPath,
            arg.Identifier,
            arg.CancellationToken
        );

    Task HandleSessionMessage(ProcessSessionMessageEventArgs arg)
        => ProcessMessageAsync(
            arg.Message,
            msg => arg.CompleteMessageAsync(msg, arg.CancellationToken),
            msg => arg.AbandonMessageAsync(msg, null, arg.CancellationToken),
            arg.FullyQualifiedNamespace,
            arg.EntityPath,
            arg.Identifier,
            arg.CancellationToken
        );

    async Task ProcessMessageAsync(
            ServiceBusReceivedMessage             msg,
            Func<ServiceBusReceivedMessage, Task> completeMessage,
            Func<ServiceBusReceivedMessage, Task> abandonMessage,
            string                                fullyQualifiedNamespace,
            string                                entityPath,
            string                                identifier,
            CancellationToken                     ct
        ) {
        if (ct.IsCancellationRequested) return;

        var eventType = (msg.ApplicationProperties.TryGetValue(Options.AttributeNames.MessageType, out var messageType) && messageType is not null
            ? messageType.ToString()
            : msg.Subject) ?? throw new InvalidOperationException("Message type is missing in message properties");
        var contentType = msg.ContentType;

        // Should this be a stream name? or topic or something
        var streamName = (msg.ApplicationProperties.TryGetValue(Options.AttributeNames.StreamName, out var stream) && stream is not null
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
    protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => _processorStrategy.Stop(cancellationToken);

    interface IServiceBusProcessorStrategy {
        ValueTask Start(CancellationToken cancellationToken);
        ValueTask Stop(CancellationToken  cancellationToken);
    }

    sealed class StandardProcessorStrategy(
            ServiceBusClient                    client,
            ServiceBusSubscriptionOptions       options,
            Func<ProcessMessageEventArgs, Task> handleMessage,
            Func<ProcessErrorEventArgs, Task>   handleError
        )
        : IServiceBusProcessorStrategy {
        ServiceBusProcessor? _processor;

        public ValueTask Start(CancellationToken cancellationToken) {
            _processor                     =  options.QueueOrTopic.MakeProcessor(client, options);
            _processor.ProcessMessageAsync += handleMessage;
            _processor.ProcessErrorAsync   += handleError;

            return new(_processor.StartProcessingAsync(cancellationToken));
        }

        public ValueTask Stop(CancellationToken cancellationToken)
            => _processor is not null
                ? new(_processor.StopProcessingAsync(cancellationToken))
                : ValueTask.CompletedTask;
    }

    sealed class SessionProcessorStrategy(
            ServiceBusClient                           client,
            ServiceBusSubscriptionOptions              options,
            Func<ProcessSessionMessageEventArgs, Task> handleSessionMessage,
            Func<ProcessErrorEventArgs, Task>          handleError
        )
        : IServiceBusProcessorStrategy {
        ServiceBusSessionProcessor? _sessionProcessor;

        public ValueTask Start(CancellationToken cancellationToken) {
            _sessionProcessor                     =  options.QueueOrTopic.MakeSessionProcessor(client, options);
            _sessionProcessor.ProcessMessageAsync += handleSessionMessage;
            _sessionProcessor.ProcessErrorAsync   += handleError;

            return new(_sessionProcessor.StartProcessingAsync(cancellationToken));
        }

        public ValueTask Stop(CancellationToken cancellationToken)
            => _sessionProcessor is not null
                ? new(_sessionProcessor.StopProcessingAsync(cancellationToken))
                : ValueTask.CompletedTask;
    }
}
