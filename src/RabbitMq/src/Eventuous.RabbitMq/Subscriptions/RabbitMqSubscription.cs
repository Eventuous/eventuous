// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eventuous.RabbitMq.Subscriptions;

/// <summary>
/// RabbitMQ subscription service
/// </summary>
[PublicAPI]
public class RabbitMqSubscription : EventSubscription<RabbitMqSubscriptionOptions> {
    public delegate ValueTask HandleEventProcessingFailure(IChannel channel, BasicDeliverEventArgs message, Exception? exception);

    readonly HandleEventProcessingFailure _failureHandler;
    readonly ConnectionFactory            _connectionFactory;

    IConnection? _connection;
    IChannel?    _channel;

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

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken).NoContext();
        _channel    = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).NoContext();

        var prefetch = Options.PrefetchCount > 0 ? Options.PrefetchCount : Options.ConcurrencyLimit * 2;
        await _channel.BasicQosAsync(0, (ushort)prefetch, false, cancellationToken).NoContext();

        var exchange = Ensure.NotEmptyString(Options.Exchange);

        Log.InfoLog?.Log("Ensuring exchange {Exchange}", exchange);

        if (string.IsNullOrWhiteSpace(Options.BindingOptions.RoutingKey) && Options.ExchangeOptions.Type == ExchangeType.Fanout) {
            Log.WarnLog?.Log("Fan-out exchange doesn't support routing keys");
        }

        await _channel.ExchangeDeclareAsync(
                exchange,
                Options.ExchangeOptions.Type,
                Options.ExchangeOptions.Durable,
                Options.ExchangeOptions.AutoDelete,
                Options.ExchangeOptions.Arguments,
                cancellationToken: cancellationToken
            )
            .NoContext();

        var queue = Options.QueueOptions.Queue ?? Options.SubscriptionId;
        Log.InfoLog?.Log("Ensuring queue {Queue}", queue);

        await _channel.QueueDeclareAsync(
                queue,
                Options.QueueOptions.Durable,
                Options.QueueOptions.Exclusive,
                Options.QueueOptions.AutoDelete,
                Options.QueueOptions.Arguments,
                cancellationToken: cancellationToken
            )
            .NoContext();

        Log.InfoLog?.Log("Binding exchange {Exchange} to queue {Queue}", exchange, queue);

        await _channel.QueueBindAsync(
                queue,
                exchange,
                Options.BindingOptions.RoutingKey,
                Options.BindingOptions.Arguments,
                cancellationToken: cancellationToken
            )
            .NoContext();

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleReceived;

        await _channel.BasicConsumeAsync(queue, false, consumer, cancellationToken).NoContext();
    }

    const string ReceivedMessageKey = "receivedMessage";

    async Task HandleReceived(object sender, BasicDeliverEventArgs received) {
        Logger.Current = Log;

        try {
            var ctx = CreateContext(sender, received).WithItem(ReceivedMessageKey, received);
            await Handler(new AsyncConsumeContext(ctx, Ack, Nack)).NoContext();
        } catch (Exception) {
            // This won't stop the subscription, but the reader will be gone. Not sure how to solve this one.
            if (Options.ThrowOnError) throw;
        }
    }

    async ValueTask Ack(IMessageConsumeContext ctx) {
        var received = ctx.Items.GetItem<BasicDeliverEventArgs>(ReceivedMessageKey)!;
        await _channel!.BasicAckAsync(received.DeliveryTag, false).NoContext();
    }

    async ValueTask Nack(IMessageConsumeContext ctx, Exception exception) {
        if (Options.ThrowOnError) throw exception;

        var received = ctx.Items.GetItem<BasicDeliverEventArgs>(ReceivedMessageKey)!;
        await _failureHandler(_channel!, received, exception).NoContext();
    }

    MessageConsumeContext CreateContext(object sender, BasicDeliverEventArgs received) {
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
            default
        );
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        if (_channel != null) {
            await _channel.CloseAsync(cancellationToken).NoContext();
            _channel.Dispose();
            _channel = null;
        }

        if (_connection != null) {
            await _connection.CloseAsync(cancellationToken: cancellationToken).NoContext();
            _connection.Dispose();
            _connection = null;
        }
    }

    async ValueTask DefaultEventFailureHandler(IChannel channel, BasicDeliverEventArgs message, Exception? exception) {
        Log.WarnLog?.Log("Error in the consumer, will redeliver", exception?.ToString() ?? "Unknown error");
        await channel.BasicRejectAsync(message.DeliveryTag, true).NoContext();
    }

    record Event(BasicDeliverEventArgs Original, IMessageConsumeContext Context);
}
