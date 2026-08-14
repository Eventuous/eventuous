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
            ? new SessionProcessorStrategy(client, Options, HandleSessionMessage, HandleError)
            : new StandardProcessorStrategy(client, Options, HandleMessage, HandleError);
    }

    /// <summary>
    /// Runs the configured error handler, then ends the run if the processor has stopped receiving for good.
    /// </summary>
    /// <remarks>
    /// The SDK's receive loop exits on a dead connection, raises this once and never restarts — untranslated,
    /// the supervisor parks forever. In a finally so a throwing user handler can't suppress the recovery.
    /// </remarks>
    async Task HandleError(SubscriptionRun run, ProcessErrorEventArgs arg) {
        try {
            await _defaultErrorHandler(arg).NoContext();
        } finally {
            if (arg is { ErrorSource: ServiceBusErrorSource.Receive, Exception: ObjectDisposedException }) {
                run.Fail(DropReason.ServerError, arg.Exception);
            }
        }
    }

    /// <summary>
    /// Starts processing the Service Bus queue or topic. The processor is recreated on every call, so its
    /// message handler is wired up here, closing over this run rather than looking one up later.
    /// </summary>
    /// <param name="run"></param>
    /// <exception cref="InvalidOperationException"></exception>
    protected override ValueTask Connect(SubscriptionRun run)
        => _processorStrategy.Start(run);

    Task HandleMessage(SubscriptionRun run, ProcessMessageEventArgs arg)
        => ProcessMessageAsync(
            run,
            arg.Message,
            msg => arg.CompleteMessageAsync(msg, arg.CancellationToken),
            msg => arg.AbandonMessageAsync(msg, null, arg.CancellationToken),
            arg.FullyQualifiedNamespace,
            arg.EntityPath,
            arg.Identifier,
            arg.CancellationToken
        );

    Task HandleSessionMessage(SubscriptionRun run, ProcessSessionMessageEventArgs arg)
        => ProcessMessageAsync(
            run,
            arg.Message,
            msg => arg.CompleteMessageAsync(msg, arg.CancellationToken),
            msg => arg.AbandonMessageAsync(msg, null, arg.CancellationToken),
            arg.FullyQualifiedNamespace,
            arg.EntityPath,
            arg.Identifier,
            arg.CancellationToken
        );

    async Task ProcessMessageAsync(
            SubscriptionRun                        run,
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
            run.NextSequence(),
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

    interface IServiceBusProcessorStrategy {
        ValueTask Start(SubscriptionRun run);
    }

    sealed class StandardProcessorStrategy(
            ServiceBusClient                                     client,
            ServiceBusSubscriptionOptions                        options,
            Func<SubscriptionRun, ProcessMessageEventArgs, Task> handleMessage,
            Func<SubscriptionRun, ProcessErrorEventArgs, Task>   handleError
        )
        : IServiceBusProcessorStrategy {
        public ValueTask Start(SubscriptionRun run) {
            var processor = options.QueueOrTopic.MakeProcessor(client, options);
            processor.ProcessMessageAsync += arg => handleMessage(run, arg);
            processor.ProcessErrorAsync   += arg => handleError(run, arg);

            run.OnDisconnect(ct => Stop(processor, ct));

            return new(processor.StartProcessingAsync(run.Token));
        }

        // Disposed in a finally because it releases the AMQP link, even if StopProcessingAsync throws.
        static async ValueTask Stop(ServiceBusProcessor processor, CancellationToken cancellationToken) {
            try {
                await processor.StopProcessingAsync(cancellationToken).NoContext();
            } finally {
                await processor.DisposeAsync().NoContext();
            }
        }
    }

    sealed class SessionProcessorStrategy(
            ServiceBusClient                                            client,
            ServiceBusSubscriptionOptions                               options,
            Func<SubscriptionRun, ProcessSessionMessageEventArgs, Task> handleSessionMessage,
            Func<SubscriptionRun, ProcessErrorEventArgs, Task>          handleError
        )
        : IServiceBusProcessorStrategy {
        public ValueTask Start(SubscriptionRun run) {
            var sessionProcessor = options.QueueOrTopic.MakeSessionProcessor(client, options);
            sessionProcessor.ProcessMessageAsync += arg => handleSessionMessage(run, arg);
            sessionProcessor.ProcessErrorAsync   += arg => handleError(run, arg);

            run.OnDisconnect(ct => Stop(sessionProcessor, ct));

            return new(sessionProcessor.StartProcessingAsync(run.Token));
        }

        // Same as the standard processor: dispose in finally, left unbounded since teardown bounds it centrally.
        static async ValueTask Stop(ServiceBusSessionProcessor sessionProcessor, CancellationToken cancellationToken) {
            try {
                await sessionProcessor.StopProcessingAsync(cancellationToken).NoContext();
            } finally {
                await sessionProcessor.DisposeAsync().NoContext();
            }
        }
    }
}
