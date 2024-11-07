// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;
using static Eventuous.Diagnostics.TelemetryTags;

namespace Eventuous.Producers;

using Diagnostics;

public abstract class BaseProducer<TProduceOptions> : IProducer<TProduceOptions> where TProduceOptions : class {
    protected BaseProducer(ProducerTracingOptions? tracingOptions = null) {
        var options = tracingOptions ?? new ProducerTracingOptions();
        DefaultTags = options.AllTags.Concat(EventuousDiagnostics.Tags).ToArray();
    }

    // ReSharper disable once MemberCanBePrivate.Global
    protected KeyValuePair<string, object?>[] DefaultTags { get; }

    protected abstract Task ProduceMessages(StreamName stream, IEnumerable<ProducedMessage> messages, TProduceOptions? options, CancellationToken cancellationToken = default);

    protected void SetActivityMessageType(string messageType) => Activity.Current?.SetTag(Message.Type, messageType);

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
}