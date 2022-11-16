// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
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
    public delegate void HandleEventProcessingFailure(
        IModel                channel,
        BasicDeliverEventArgs message,
        Exception?            exception
    );

    readonly HandleEventProcessingFailure _failureHandler;
    readonly IConnection                  _connection;
    readonly IModel                       _channel;

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="options"></param>
    /// <param name="consumePipe"></param>
    /// <param name="loggerFactory"></param>
    public RabbitMqSubscription(
        ConnectionFactory                     connectionFactory,
        IOptions<RabbitMqSubscriptionOptions> options,
        ConsumePipe                           consumePipe,
        ILoggerFactory?                       loggerFactory
    ) : this(connectionFactory, options.Value, consumePipe, loggerFactory) { }

    /// <summary>
    /// Creates RabbitMQ subscription service instance
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="options"></param>
    /// <param name="consumePipe"></param>
    /// <param name="loggerFactory"></param>
    public RabbitMqSubscription(
        ConnectionFactory           connectionFactory,
        RabbitMqSubscriptionOptions options,
        ConsumePipe                 consumePipe,
        ILoggerFactory?             loggerFactory
    )
        : base(
            Ensure.NotNull(options),
            consumePipe.AddFilterFirst(new AsyncHandlingFilter(options.ConcurrencyLimit * 10)),
            loggerFactory
        ) {
        _failureHandler = options.FailureHandler ?? DefaultEventFailureHandler;
        _connection     = Ensure.NotNull(connectionFactory).CreateConnection();
        _channel        = _connection.CreateModel();

        var prefetch = Options.PrefetchCount > 0 ? Options.PrefetchCount : Options.ConcurrencyLimit * 2;

        _channel.BasicQos(0, (ushort)prefetch, false);

        if (options.FailureHandler != null && !options.ThrowOnError) Log.ThrowOnErrorIncompatible();
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
        new RabbitMqSubscriptionOptions
            { Exchange = exchange, SubscriptionId = subscriptionId, EventSerializer = eventSerializer },
        consumePipe,
        loggerFactory
    ) { }

    protected override ValueTask Subscribe(CancellationToken cancellationToken) {
        var exchange = Ensure.NotEmptyString(Options.Exchange);

        Log.InfoLog?.Log("Ensuring exchange", exchange);

        _channel.ExchangeDeclare(
            exchange,
            Options.ExchangeOptions?.Type       ?? ExchangeType.Fanout,
            Options.ExchangeOptions?.Durable    ?? true,
            Options.ExchangeOptions?.AutoDelete ?? true,
            Options.ExchangeOptions?.Arguments
        );

        Log.InfoLog?.Log("Ensuring queue", Options.SubscriptionId);

        _channel.QueueDeclare(
            Options.SubscriptionId,
            Options.QueueOptions?.Durable    ?? true,
            Options.QueueOptions?.Exclusive  ?? false,
            Options.QueueOptions?.AutoDelete ?? false,
            Options.QueueOptions?.Arguments
        );

        Log.InfoLog?.Log("Binding exchange to queue:", exchange, Options.SubscriptionId);

        _channel.QueueBind(
            Options.SubscriptionId,
            exchange,
            Options.BindingOptions?.RoutingKey ?? "",
            Options.BindingOptions?.Arguments
        );

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleReceived;

        _channel.BasicConsume(consumer, Options.SubscriptionId);

        return default;
    }

    const string ReceivedMessageKey = "receivedMessage";

    async Task HandleReceived(object sender, BasicDeliverEventArgs received) {
        Logger.Current = Log;

        try {
            var ctx = CreateContext(sender, received).WithItem(ReceivedMessageKey, received);
            await Handler(new AsyncConsumeContext(ctx, Ack, Nack)).NoContext();
        }
        catch (Exception) {
            // This won't stop the subscription, but the reader will be gone. Not sure how to solve this one.
            if (Options.ThrowOnError) throw;
        }
    }

    ValueTask Ack(IMessageConsumeContext ctx) {
        var received = ctx.Items.GetItem<BasicDeliverEventArgs>(ReceivedMessageKey)!;
        _channel.BasicAck(received.DeliveryTag, false);
        return default;
    }

    ValueTask Nack(IMessageConsumeContext ctx, Exception exception) {
        if (Options.ThrowOnError) throw exception;

        var received = ctx.Items.GetItem<BasicDeliverEventArgs>(ReceivedMessageKey)!;
        _failureHandler(_channel, received, exception);
        return default;
    }

    IMessageConsumeContext CreateContext(object sender, BasicDeliverEventArgs received) {
        var evt = DeserializeData(
            received.BasicProperties.ContentType,
            received.BasicProperties.Type,
            received.Body,
            received.Exchange
        );

        var meta = received.BasicProperties.Headers != null
            ? new Metadata(received.BasicProperties.Headers.ToDictionary(x => x.Key, x => x.Value)!)
            : null;

        return new MessageConsumeContext(
            received.BasicProperties.MessageId,
            received.BasicProperties.Type,
            received.BasicProperties.ContentType,
            received.Exchange,
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

    protected override ValueTask Unsubscribe(CancellationToken cancellationToken) {
        _channel.Close();
        _channel.Dispose();
        _connection.Close();
        _connection.Dispose();

        return default;
    }

    void DefaultEventFailureHandler(IModel channel, BasicDeliverEventArgs message, Exception? exception) {
        Log.WarnLog?.Log("Error in the consumer, will redeliver", exception?.ToString() ?? "Unknown error");
        _channel.BasicReject(message.DeliveryTag, true);
    }

    record Event(BasicDeliverEventArgs Original, IMessageConsumeContext Context);
}
