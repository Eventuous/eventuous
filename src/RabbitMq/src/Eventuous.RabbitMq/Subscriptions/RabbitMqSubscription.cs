// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;

namespace Eventuous.RabbitMq.Subscriptions;

/// <summary>
/// RabbitMQ subscription service
/// </summary>
[PublicAPI]
public class RabbitMqSubscription : EventSubscription<RabbitMqSubscriptionOptions> {
    public delegate ValueTask HandleEventProcessingFailure(IChannel channel, BasicDeliverEventArgs message, Exception? exception);

    readonly HandleEventProcessingFailure _failureHandler;
    readonly ConnectionFactory            _connectionFactory;

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory">RabbitMQ connection factory</param>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe">Pre-constructed consume pipe</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <param name="eventSerializer">Event serializer</param>
    public RabbitMqSubscription(
            ConnectionFactory                     connectionFactory,
            IOptions<RabbitMqSubscriptionOptions> options,
            ConsumePipe                           consumePipe,
            ILoggerFactory?                       loggerFactory,
            IEventSerializer?                     eventSerializer = null
        ) : this(connectionFactory, options.Value, consumePipe, loggerFactory, eventSerializer) { }

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory">RabbitMQ connection factory</param>
    /// <param name="options"></param>
    /// <param name="consumePipe"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="eventSerializer"></param>
    public RabbitMqSubscription(
            ConnectionFactory           connectionFactory,
            RabbitMqSubscriptionOptions options,
            ConsumePipe                 consumePipe,
            ILoggerFactory?             loggerFactory,
            IEventSerializer?           eventSerializer = null
        )
        : base(
            Ensure.NotNull(options),
            consumePipe.AddFilterFirst(new AsyncHandlingFilter(options.ConcurrencyLimit)),
            loggerFactory,
            eventSerializer
        ) {
        _failureHandler    = options.FailureHandler ?? DefaultEventFailureHandler;
        _connectionFactory = Ensure.NotNull(connectionFactory);

        if (options is { FailureHandler: not null, ThrowOnError: false }) Log.ThrowOnErrorIncompatible();
    }

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory">RabbitMQ connection factory</param>
    /// <param name="exchange">Exchange to consume events from, the queue will get bound to this exchange</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="consumePipe"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    public RabbitMqSubscription(
            ConnectionFactory connectionFactory,
            string            exchange,
            string            subscriptionId,
            ConsumePipe       consumePipe,
            ILoggerFactory?   loggerFactory,
            IEventSerializer? eventSerializer = null
        ) : this(
        connectionFactory,
        new RabbitMqSubscriptionOptions { Exchange = exchange, SubscriptionId = subscriptionId },
        consumePipe,
        loggerFactory,
        eventSerializer
    ) { }

    protected override async ValueTask Connect(SubscriptionRun run) {
        // Registered as each handle opens, so a partial Connect still leaves teardown able to close what
        // it opened, channel before connection.
        var connection = await _connectionFactory.CreateConnectionAsync(run.Token).NoContext();
        run.OnDisconnect(CloseConnection);

        var channel = await connection.CreateChannelAsync(cancellationToken: run.Token).NoContext();
        run.OnDisconnect(CloseChannel);

        var prefetch = Options.PrefetchCount > 0 ? Options.PrefetchCount : Options.ConcurrencyLimit * 2;
        await channel.BasicQosAsync(0, (ushort)prefetch, false, run.Token).NoContext();

        var exchange = Ensure.NotEmptyString(Options.Exchange);

        Log.InfoLog?.Log("Ensuring exchange {Exchange}", exchange);

        if (string.IsNullOrWhiteSpace(Options.BindingOptions.RoutingKey) && Options.ExchangeOptions.Type == ExchangeType.Fanout) {
            Log.WarnLog?.Log("Fan-out exchange doesn't support routing keys");
        }

        await channel.ExchangeDeclareAsync(
                exchange,
                Options.ExchangeOptions.Type,
                Options.ExchangeOptions.Durable,
                Options.ExchangeOptions.AutoDelete,
                Options.ExchangeOptions.Arguments,
                cancellationToken: run.Token
            )
            .NoContext();

        var queue = Options.QueueOptions.Queue ?? Options.SubscriptionId;
        Log.InfoLog?.Log("Ensuring queue {Queue}", queue);

        await channel.QueueDeclareAsync(
                queue,
                Options.QueueOptions.Durable,
                Options.QueueOptions.Exclusive,
                Options.QueueOptions.AutoDelete,
                Options.QueueOptions.Arguments,
                cancellationToken: run.Token
            )
            .NoContext();

        Log.InfoLog?.Log("Binding exchange {Exchange} to queue {Queue}", exchange, queue);

        await channel.QueueBindAsync(
                queue,
                exchange,
                Options.BindingOptions.RoutingKey,
                Options.BindingOptions.Arguments,
                cancellationToken: run.Token
            )
            .NoContext();

        // Channel captured as a local rather than looked up at ack time, since a delivery tag only means
        // something on the channel it came from, and a resubscribe would have moved on to a different one.
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, received) => HandleReceived(run, channel, received);

        await channel.BasicConsumeAsync(queue, false, consumer, run.Token).NoContext();

        return;

        // Each disposal is in its own finally: closes tend to fail exactly when the broker is unhealthy,
        // which is when a leak costs most, and a throwing channel close must not skip the connection close.
        async ValueTask CloseChannel(CancellationToken cancellationToken) {
            try {
                await channel.CloseAsync(cancellationToken).NoContext();
            } finally {
                channel.Dispose();
            }
        }

        async ValueTask CloseConnection(CancellationToken cancellationToken) {
            try {
                await connection.CloseAsync(cancellationToken: cancellationToken).NoContext();
            } finally {
                connection.Dispose();
            }
        }
    }

    /// <summary>
    /// Failures end this run instead of being thrown. A throw here reaches the client's consumer dispatcher,
    /// which routes it to <c>CallbackException</c> and carries on, so under <c>ThrowOnError</c> the
    /// subscription kept running as if nothing had happened.
    /// </summary>
    async Task HandleReceived(SubscriptionRun run, IChannel channel, BasicDeliverEventArgs received) {
        Logger.Current = Log;

        try {
            var ctx = CreateContext(received, run.Token);
            await Handler(new AsyncConsumeContext(ctx, Ack, Nack)).NoContext();
        } catch (Exception e) {
            if (Options.ThrowOnError) run.Fail(DropReason.SubscriptionError, e);
        }

        return;

        async ValueTask Ack(IMessageConsumeContext _) {
            try {
                await channel.BasicAckAsync(received.DeliveryTag, false).NoContext();
            } catch (Exception e) when (IsChannelGone(e)) { LogDeliveryUndecided(e); }
        }

        async ValueTask Nack(IMessageConsumeContext _, Exception exception) {
            // The broker is told first, and told whatever ThrowOnError says, because deciding the delivery is
            // what the failure handler is for: leaving it to the channel close would requeue it no matter what
            // the handler was configured to do with it. Rejecting before the run ends also keeps the channel
            // alive for the call.
            try {
                await _failureHandler(channel, received, exception).NoContext();
            } catch (Exception e) when (IsChannelGone(e)) {
                LogDeliveryUndecided(e);
            } finally {
                // Then the run ends, which is what ThrowOnError means here. In a finally because a failure
                // handler is user code: one that throws anything else would otherwise skip this, and the
                // filter would log that throw and leave the run looking healthy with nothing consuming.
                // Reported rather than thrown, since a throw on the filter's channel worker kills the reader
                // and nothing observes its task until dispose.
                if (Options.ThrowOnError) run.Fail(DropReason.SubscriptionError, exception);
            }
        }

        void LogDeliveryUndecided(Exception e)
            => Log.WarnLog?.Log(e, "Delivery {DeliveryTag} left undecided, its channel is already closed", received.DeliveryTag);
    }

    /// <summary>
    /// Whether a failed ack/nack means the channel is gone (a buffered handler finishing after teardown
    /// closed it) rather than a broker refusal. Safe to swallow: an unacked delivery just gets redelivered.
    /// </summary>
    static bool IsChannelGone(Exception exception) => exception is AlreadyClosedException or ObjectDisposedException;

    MessageConsumeContext CreateContext(BasicDeliverEventArgs received, CancellationToken cancellationToken) {
        var evt = DeserializeData(received.BasicProperties.ContentType!, received.BasicProperties.Type!, received.Body, received.Exchange);

        var meta = received.BasicProperties.Headers != null
            ? new Metadata(received.BasicProperties.Headers.ToDictionary(x => x.Key, x => x.Value)!)
            : null;

        return new(
            received.BasicProperties.MessageId!,
            received.BasicProperties.Type!,
            received.BasicProperties.ContentType!,
            received.Exchange,
            0,
            0,
            0,
            received.DeliveryTag,
            received.BasicProperties.Timestamp.ToDateTime(),
            evt,
            meta,
            SubscriptionId,
            cancellationToken
        );
    }

    async ValueTask DefaultEventFailureHandler(IChannel channel, BasicDeliverEventArgs message, Exception? exception) {
        Log.WarnLog?.Log("Error in the consumer, will redeliver", exception?.ToString() ?? "Unknown error");
        await channel.BasicRejectAsync(message.DeliveryTag, true).NoContext();
    }

    record Event(BasicDeliverEventArgs Original, IMessageConsumeContext Context);
}
