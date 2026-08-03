// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Eventuous.RabbitMq.Shared;
using Microsoft.Extensions.Logging;

namespace Eventuous.RabbitMq.Producers;

using Diagnostics;

/// <summary>
/// RabbitMQ producer
/// </summary>
public class RabbitMqProducer : BaseProducer<RabbitMqProduceOptions>, IHostedProducer {
    readonly ILogger<RabbitMqProducer>? _log;
    readonly RabbitMqExchangeOptions?   _options;
    readonly IEventSerializer           _serializer;
    readonly ConnectionFactory          _connectionFactory;
    readonly ExchangeCache              _exchangeCache;

    IConnection? _connection;
    IChannel?    _channel;

    /// <summary>
    /// Creates a RabbitMQ producer instance
    /// </summary>
    /// <param name="connectionFactory">RabbitMQ connection factory</param>
    /// <param name="serializer">Optional event serializer instance</param>
    /// <param name="log">Optional logger</param>
    /// <param name="options">Optional additional configuration for the exchange</param>
    public RabbitMqProducer(
            ConnectionFactory          connectionFactory,
            IEventSerializer?          serializer = null,
            ILogger<RabbitMqProducer>? log        = null,
            RabbitMqExchangeOptions?   options    = null
        )
        : base(TracingOptions) {
        _log               = log;
        _options           = options;
        _serializer        = serializer ?? EventSerializer.Default;
        _connectionFactory = Ensure.NotNull(connectionFactory);
        _exchangeCache     = new(_log);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default) {
        var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
        _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken).NoContext();
        _channel    = await _connection.CreateChannelAsync(channelOptions, cancellationToken).NoContext();
        Ready       = true;
    }

    static readonly ProducerTracingOptions TracingOptions = new() {
        MessagingSystem  = "rabbitmq",
        DestinationKind  = "exchange",
        ProduceOperation = "publish"
    };

    protected override async Task ProduceMessages(
            StreamName                   stream,
            IEnumerable<ProducedMessage> messages,
            RabbitMqProduceOptions?      options,
            CancellationToken            cancellationToken = default
        ) {
        await EnsureExchange(stream, cancellationToken).NoContext();
        var produced = new List<ProducedMessage>();
        var failed   = new List<(ProducedMessage Msg, Exception Ex)>();
        var pending  = new List<(ProducedMessage Msg, Task Publish)>();

        foreach (var message in messages) {
            if (Activity.Current is { IsAllDataRequested: true }) {
                Activity.Current.SetTag(RabbitMqTelemetryTags.RoutingKey, options?.RoutingKey);
            }

            pending.Add((message, Publish(stream, message, options, cancellationToken)));
        }

        foreach (var (message, publish) in pending) {
            try {
                await publish.NoContext();
                produced.Add(message);
            } catch (Exception e) {
                _log?.LogError(e, "Failed to produce message to RabbitMQ");
                failed.Add((message, e));
            }
        }

        await produced.Select(x => x.Ack<RabbitMqProducer>()).WhenAll().NoContext();

        await failed
            .Select(x => x.Msg.Nack<RabbitMqProducer>("Failed to produce to RabbitMQ", x.Ex))
            .WhenAll()
            .NoContext();
    }

    async Task Publish(string stream, ProducedMessage message, RabbitMqProduceOptions? options, CancellationToken cancellationToken) {
        if (_channel == null) throw new InvalidOperationException("Producer hasn't been initialized, call Initialize");

        var (msg, metadata)                   = (message.Message, message.Metadata);
        var (eventType, contentType, payload) = _serializer.SerializeEvent(msg);

        SetActivityMessageType(eventType);

        var prop = new BasicProperties {
            ContentType   = contentType,
            Persistent    = options?.Persisted != false,
            Type          = eventType,
            CorrelationId = metadata!.GetCorrelationId(),
            MessageId     = message.MessageId.ToString()
        };

        metadata!.Remove(MetaTags.MessageId);
        prop.Headers = metadata.ToDictionary(x => x.Key, x => x.Value);

        if (options != null) {
            prop.Expiration = options.Expiration?.ToString();
            prop.Priority   = options.Priority;
            prop.AppId      = options.AppId;
            prop.ReplyTo    = options.ReplyTo;
        }

        await _channel.BasicPublishAsync(stream, options?.RoutingKey ?? "", true, prop, payload, cancellationToken).NoContext();
    }

    Task EnsureExchange(string exchange, CancellationToken cancellationToken)
        => _exchangeCache.EnsureExchange(
            exchange,
            () =>
                _channel!.ExchangeDeclareAsync(
                    exchange,
                    _options?.Type       ?? ExchangeType.Fanout,
                    _options?.Durable    ?? true,
                    _options?.AutoDelete ?? false,
                    _options?.Arguments,
                    cancellationToken: cancellationToken
                )
        );

    public async Task StopAsync(CancellationToken cancellationToken = default) {
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

    public bool Ready { get; private set; }
}
