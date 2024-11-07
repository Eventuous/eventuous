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
    IModel?      _channel;

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
        _serializer        = serializer ?? DefaultEventSerializer.Instance;
        _connectionFactory = Ensure.NotNull(connectionFactory);
        _exchangeCache     = new(_log);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) {
        _connection = _connectionFactory.CreateConnection();
        _channel    = _connection.CreateModel();
        _channel.ConfirmSelect();
        Ready = true;

        return Task.CompletedTask;
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
        EnsureExchange(stream);
        var produced = new List<ProducedMessage>();
        var failed   = new List<(ProducedMessage Msg, Exception Ex)>();

        foreach (var message in messages) {
            if (Activity.Current is { IsAllDataRequested: true }) {
                Activity.Current.SetTag(RabbitMqTelemetryTags.RoutingKey, options?.RoutingKey);
            }

            try {
                Publish(stream, message, options);
                produced.Add(message);
            } catch (Exception e) {
                _log?.LogError(e, "Failed to produce message to RabbitMQ");
                failed.Add((message, e));
            }
        }

        await Confirm(cancellationToken).NoContext();

        await produced.Select(x => x.Ack<RabbitMqProducer>()).WhenAll().NoContext();

        await failed
            .Select(x => x.Msg.Nack<RabbitMqProducer>("Failed to produce to RabbitMQ", x.Ex))
            .WhenAll()
            .NoContext();
    }

    void Publish(string stream, ProducedMessage message, RabbitMqProduceOptions? options) {
        if (_channel == null) throw new InvalidOperationException("Producer hasn't been initialized, call Initialize");

        var (msg, metadata)                   = (message.Message, message.Metadata);
        var (eventType, contentType, payload) = _serializer.SerializeEvent(msg);

        SetActivityMessageType(eventType);
        var prop = _channel.CreateBasicProperties();
        prop.ContentType   = contentType;
        prop.Persistent    = options?.Persisted != false;
        prop.Type          = eventType;
        prop.CorrelationId = metadata!.GetCorrelationId();
        prop.MessageId     = message.MessageId.ToString();

        metadata!.Remove(MetaTags.MessageId);
        prop.Headers = metadata.ToDictionary(x => x.Key, x => x.Value);

        if (options != null) {
            prop.Expiration = options.Expiration?.ToString();
            prop.Priority   = options.Priority;
            prop.AppId      = options.AppId;
            prop.ReplyTo    = options.ReplyTo;
        }

        _channel.BasicPublish(stream, options?.RoutingKey ?? "", true, prop, payload);
    }

    void EnsureExchange(string exchange)
        => _exchangeCache.EnsureExchange(
            exchange,
            () =>
                _channel!.ExchangeDeclare(
                    exchange,
                    _options?.Type       ?? ExchangeType.Fanout,
                    _options?.Durable    ?? true,
                    _options?.AutoDelete ?? false,
                    _options?.Arguments
                )
        );

    async Task Confirm(CancellationToken cancellationToken) {
        while (!_channel!.WaitForConfirms(ConfirmTimeout) && !cancellationToken.IsCancellationRequested) {
            await Task.Delay(ConfirmIdle, cancellationToken).NoContext();
        }
    }

    static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(1);
    static readonly TimeSpan ConfirmIdle    = TimeSpan.FromMilliseconds(100);

    public Task StopAsync(CancellationToken cancellationToken = default) {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();

        return Task.CompletedTask;
    }

    public bool Ready { get; private set; }
}
