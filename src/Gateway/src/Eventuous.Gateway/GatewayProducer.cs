// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Gateway;

class GatewayProducer<T>(IProducer<T> inner) : IProducer<T> where T : class {
    readonly bool _isHostedService = inner is not IHostedProducer;

    public async Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, T? options, CancellationToken cancellationToken = default) {
        if (_isHostedService) { await WaitForInner(inner, cancellationToken).NoContext(); }

        await inner.Produce(stream, messages, options, cancellationToken).NoContext();
    }

    static async ValueTask WaitForInner(IProducer<T> inner, CancellationToken cancellationToken) {
        if (inner is not IHostedProducer hosted) return;

        while (!hosted.Ready) {
            // EventuousEventSource.Log.Warn("Producer not ready, waiting...");
            await Task.Delay(1000, cancellationToken).NoContext();
        }
    }

    public Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, CancellationToken cancellationToken = default)
        => Produce(stream, messages, null, cancellationToken);
}
