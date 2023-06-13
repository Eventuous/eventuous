// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Gateway;

class GatewayProducer<T> : IEventProducer<T> where T : class {
    readonly IEventProducer<T> _inner;
    readonly bool              _isHostedService;

    public GatewayProducer(IEventProducer<T> inner) {
        _isHostedService = inner is not IHostedProducer;
        _inner           = inner;
    }

    public async Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, T? options, CancellationToken cancellationToken = default) {
        if (_isHostedService) {
            await WaitForInner(_inner, cancellationToken).NoContext();
        }

        await _inner.Produce(stream, messages, options, cancellationToken).NoContext();
    }

    static async ValueTask WaitForInner(IEventProducer<T> inner, CancellationToken cancellationToken) {
        if (inner is not IHostedProducer hosted) return;

        while (!hosted.Ready) {
            // EventuousEventSource.Log.Warn("Producer not ready, waiting...");
            await Task.Delay(1000, cancellationToken).NoContext();
        }
    }

    public Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, CancellationToken cancellationToken = default)
        => Produce(stream, messages, null, cancellationToken);
}
