// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Gateway;

class GatewayProducer<T> : GatewayProducer, IEventProducer<T> where T : class {
    readonly IEventProducer<T> _inner;

    public GatewayProducer(IEventProducer<T> inner) : base(inner)
        => _inner = inner;

    public async Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, T? options, CancellationToken cancellationToken = default) {
        if (_isHostedService) {
            await WaitForInner(_inner, cancellationToken).NoContext();
        }

        await _inner.Produce(stream, messages, options, cancellationToken).NoContext();
    }
}

class GatewayProducer : IEventProducer {
    readonly IEventProducer _inner;

    protected readonly bool _isHostedService;

    public GatewayProducer(IEventProducer inner) {
        _isHostedService = inner is not IHostedProducer;
        _inner           = inner;
    }

    public async Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, CancellationToken cancellationToken = default) {
        if (_isHostedService) {
            await WaitForInner(_inner, cancellationToken).NoContext();
        }

        await _inner.Produce(stream, messages, cancellationToken).NoContext();
    }

    protected static async ValueTask WaitForInner(IEventProducer inner, CancellationToken cancellationToken) {
        if (inner is not IHostedProducer hosted) return;

        while (!hosted.Ready) {
            // EventuousEventSource.Log.Warn("Producer not ready, waiting...");
            await Task.Delay(1000, cancellationToken).NoContext();
        }
    }
}
