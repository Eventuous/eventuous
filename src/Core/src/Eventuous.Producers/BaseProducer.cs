// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;
using static Eventuous.Diagnostics.TelemetryTags;

namespace Eventuous.Producers;

using Diagnostics;

/// <summary>
/// Base class for all producers
/// </summary>
/// <typeparam name="TProduceOptions">Produce options type</typeparam>
public abstract class BaseProducer<TProduceOptions> : IProducer<TProduceOptions> where TProduceOptions : class {
    /// <summary>
    /// Creates a new instance of the producer
    /// </summary>
    /// <param name="tracingOptions">Tracing options for the producer</param>
    protected BaseProducer(ProducerTracingOptions? tracingOptions = null) {
        var options = tracingOptions ?? new ProducerTracingOptions();
        DefaultTags = [.. options.AllTags, .. EventuousDiagnostics.Tags];
    }

    /// <summary>
    /// Default tags for the producer activity
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    protected KeyValuePair<string, object?>[] DefaultTags { get; }

    /// <summary>
    /// Sets the message type tag for the current activity
    /// </summary>
    /// <param name="messageType">Message type string</param>
    protected void SetActivityMessageType(string messageType) => Activity.Current?.SetTag(Message.Type, messageType);

    /// <summary>
    /// Internal method to produce messages to the store. Must be implemented by the actual producer.
    /// </summary>
    /// <param name="stream">Stream name where the messages should be produced</param>
    /// <param name="messages">Collection of messages to produce</param>
    /// <param name="options">Produce options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    protected abstract Task ProduceMessages(StreamName stream, IEnumerable<ProducedMessage> messages, TProduceOptions? options, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, CancellationToken cancellationToken = default)
        => Produce(stream, messages, null, cancellationToken);

    /// <inheritdoc />
    public async Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, TProduceOptions? options, CancellationToken cancellationToken = default) {
        var messagesArray = messages.ToArray();
        if (messagesArray.Length == 0) return;

        var traced = messagesArray.Length == 1 ? ForOne() : ProducerActivity.Start(messagesArray, DefaultTags);

        using var activity = traced.act;

        if (activity is { IsAllDataRequested: true }) {
            activity.SetTag(Messaging.Destination, stream.ToString());
            activity.SetTag(TelemetryTags.Eventuous.Stream, stream.ToString());
        }

        await ProduceMessages(stream, traced.msgs, options, cancellationToken).NoContext();

        return;

        (Activity? act, ProducedMessage[] msgs) ForOne() {
            var (act, producedMessage) = ProducerActivity.Start(messagesArray[0], DefaultTags);
            return (act, [producedMessage]);
        }
    }

    /// <summary>
    /// Produces a collection of requests in parallel
    /// </summary>
    /// <param name="requests">Collection of produce requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    public Task Produce(IReadOnlyCollection<ProduceRequest<TProduceOptions>> requests, CancellationToken cancellationToken = default) {
        return requests.Count == 0 ? Task.CompletedTask : Task.WhenAll(requests.Select(r => Produce(r.Stream, r.Messages, r.Options, cancellationToken)));
    }

    /// <inheritdoc />
    public Task Produce(IReadOnlyCollection<ProduceRequest> requests, CancellationToken cancellationToken = default) {
        return requests.Count == 0 ? Task.CompletedTask : Task.WhenAll(requests.Select(r => Produce(r.Stream, r.Messages, cancellationToken)));
    }
}
