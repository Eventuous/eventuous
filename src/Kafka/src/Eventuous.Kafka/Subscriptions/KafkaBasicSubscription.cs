// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eventuous.Kafka.Subscriptions;

/// <summary>
/// Kafka subscription service that consumes messages with byte[] payload without using the schema registry.
/// The message type is specified in the headers, so the type mapping is required.
/// </summary>
[PublicAPI]
public class KafkaBasicSubscription : EventSubscription<KafkaSubscriptionOptions> {
    const string DynamicSerializationMessage = "Using dynamic serialization";

    readonly HandleConsumeError          _failureHandler;
    readonly IConsumer<string, byte[]>   _consumer;
    CancellationTokenSource?             _consumerCts;
    Task?                                _consumeTask;

    /// <summary>
    /// Creates Kafka subscription service instance
    /// </summary>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe">Pre-constructed consume pipe</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <param name="eventSerializer">Event serializer</param>
    public KafkaBasicSubscription(
            IOptions<KafkaSubscriptionOptions> options,
            ConsumePipe                        consumePipe,
            ILoggerFactory?                    loggerFactory,
            IEventSerializer?                  eventSerializer = null
        ) : this(options.Value, consumePipe, loggerFactory, eventSerializer) { }

    /// <summary>
    /// Creates Kafka subscription service instance
    /// </summary>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe">Pre-constructed consume pipe</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <param name="eventSerializer">Event serializer</param>
    public KafkaBasicSubscription(
            KafkaSubscriptionOptions options,
            ConsumePipe              consumePipe,
            ILoggerFactory?          loggerFactory,
            IEventSerializer?        eventSerializer = null
        )
        : base(
            Ensure.NotNull(options),
            consumePipe.AddFilterFirst(new AsyncHandlingFilter(options.ConcurrencyLimit)),
            loggerFactory,
            eventSerializer
        ) {
        _failureHandler = options.FailureHandler ?? DefaultFailureHandler;

        var consumerConfig = Ensure.NotNull(options.ConsumerConfig);

        // Ensure GroupId is set, defaulting to SubscriptionId if not provided
        if (string.IsNullOrWhiteSpace(consumerConfig.GroupId)) {
            consumerConfig.GroupId = options.SubscriptionId;
        }

        _consumer = new ConsumerBuilder<string, byte[]>(consumerConfig)
            .SetErrorHandler((_, error) => {
                if (error.IsFatal) {
                    Log.WarnLog?.Log("Fatal Kafka error: {Error}", error.Reason);
                    Dropped(DropReason.ServerError, new KafkaException(error));
                }
                else {
                    Log.WarnLog?.Log("Kafka error: {Error}", error.Reason);
                }
            })
            .Build();

        if (options is { FailureHandler: not null, ThrowOnError: false }) {
            Log.WarnLog?.Log("ThrowOnError is false but custom FailureHandler is set. FailureHandler will be called on errors.");
        }
    }

    const string ConsumeResultKey = "consumeResult";

    [RequiresUnreferencedCode(DynamicSerializationMessage)]
    [RequiresDynamicCode(DynamicSerializationMessage)]
    protected override ValueTask Subscribe(CancellationToken cancellationToken) {
        var topic = Ensure.NotEmptyString(Options.Topic);

        Log.InfoLog?.Log("Subscribing to Kafka topic {Topic}", topic);

        _consumer.Subscribe(topic);

        _consumerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumeTask = Task.Run(() => ConsumeLoop(_consumerCts.Token), _consumerCts.Token);

        return default;
    }

    [RequiresUnreferencedCode(DynamicSerializationMessage)]
    [RequiresDynamicCode(DynamicSerializationMessage)]
    async Task ConsumeLoop(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                var result = _consumer.Consume(cancellationToken);

                if (result?.Message == null) continue;

                Logger.Current = Log;
                await HandleConsumed(result).NoContext();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                // Normal shutdown
                break;
            }
            catch (ConsumeException ex) {
                Log.WarnLog?.Log("Kafka consume error: {Error}", ex.Error.Reason);

                if (ex.Error.IsFatal) {
                    Dropped(DropReason.ServerError, ex);
                    break;
                }
            }
            catch (Exception ex) {
                Log.WarnLog?.Log("Unexpected error in Kafka consume loop: {Error}", ex.Message);

                if (Options.ThrowOnError) throw;
            }
        }
    }

    [RequiresUnreferencedCode(DynamicSerializationMessage)]
    [RequiresDynamicCode(DynamicSerializationMessage)]
    async Task HandleConsumed(ConsumeResult<string, byte[]> result) {
        try {
            var ctx = CreateContext(result).WithItem(ConsumeResultKey, result);
            var asyncCtx = new AsyncConsumeContext(ctx, Ack, Nack);
            await Handler(asyncCtx).NoContext();
        }
        catch (Exception ex) {
            if (Options.ThrowOnError) throw;

            Log.WarnLog?.Log("Error handling Kafka message: {Error}", ex.Message);
        }
    }

    ValueTask Ack(IMessageConsumeContext ctx) {
        var result = ctx.Items.GetItem<ConsumeResult<string, byte[]>>(ConsumeResultKey)!;

        try {
            _consumer.Commit(result);
        }
        catch (KafkaException ex) {
            Log.WarnLog?.Log("Failed to commit Kafka offset: {Error}", ex.Error.Reason);
        }

        return default;
    }

    ValueTask Nack(IMessageConsumeContext ctx, Exception exception) {
        if (Options.ThrowOnError) throw exception;

        var result = ctx.Items.GetItem<ConsumeResult<string, byte[]>>(ConsumeResultKey)!;
        _failureHandler(_consumer, result, exception);

        return default;
    }

    [RequiresUnreferencedCode(DynamicSerializationMessage)]
    [RequiresDynamicCode(DynamicSerializationMessage)]
    MessageConsumeContext CreateContext(ConsumeResult<string, byte[]> result) {
        var headers = result.Message.Headers;

        var messageType = GetHeaderValue(headers, KafkaHeaderKeys.MessageTypeHeader) ?? "unknown";
        var contentType = GetHeaderValue(headers, KafkaHeaderKeys.ContentTypeHeader) ?? "application/json";

        var evt = DeserializeData(
            contentType,
            messageType,
            result.Message.Value,
            result.Topic,
            (ulong)result.Offset.Value
        );

        var meta = headers.AsMetadata();

        return new MessageConsumeContext(
            result.Message.Key ?? Guid.NewGuid().ToString(),
            messageType,
            contentType,
            result.Topic,
            (ulong)result.Offset.Value,
            (ulong)result.Offset.Value,
            (ulong)result.Offset.Value,
            Interlocked.Increment(ref Sequence),
            result.Message.Timestamp.UtcDateTime,
            evt,
            meta,
            SubscriptionId,
            Stopping.Token
        );
    }

    static string? GetHeaderValue(Headers headers, string key) {
        var header = headers.FirstOrDefault(h => h.Key == key);
        return header == null ? null : Encoding.UTF8.GetString(header.GetValueBytes());
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        Log.InfoLog?.Log("Unsubscribing from Kafka topic {Topic}", Options.Topic);

        if (_consumerCts != null) {
            await _consumerCts.CancelAsync();

            if (_consumeTask != null) {
                try {
                    await _consumeTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).NoContext();
                }
                catch (TimeoutException) {
                    Log.WarnLog?.Log("Kafka consume task did not complete within timeout");
                }
                catch (OperationCanceledException) {
                    // Expected
                }
            }

            _consumerCts.Dispose();
            _consumerCts = null;
        }

        _consumer.Unsubscribe();
        _consumer.Close();
        _consumer.Dispose();
    }

    void DefaultFailureHandler(IConsumer<string, byte[]> consumer, ConsumeResult<string, byte[]> result, Exception? exception) {
        Log.WarnLog?.Log(
            "Error processing Kafka message from topic {Topic}, partition {Partition}, offset {Offset}: {Error}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            exception?.Message ?? "Unknown error"
        );
        // By default, we don't seek back - the message is lost.
        // Users can implement custom FailureHandler for different behavior.
    }
}
