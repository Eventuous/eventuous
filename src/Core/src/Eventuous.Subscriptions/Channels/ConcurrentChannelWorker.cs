// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

public sealed class ConcurrentChannelWorker<T> : IAsyncDisposable {
    readonly Channel<T>              _channel;
    readonly CancellationTokenSource _cts;
    readonly Task[]                  _readerTasks;

    /// <summary>
    /// Creates a new instance of the channel worker, starts a task for background reads
    /// </summary>
    /// <param name="channel">Channel to use for writes and reads</param>
    /// <param name="process">Function to process each element the worker reads from the channel</param>
    /// <param name="concurrencyLevel"></param>
    public ConcurrentChannelWorker(
        Channel<T>        channel,
        ProcessElement<T> process,
        int               concurrencyLevel
    ) {
        _channel = channel;
        _cts     = new CancellationTokenSource();

        _readerTasks = Enumerable.Range(0, concurrencyLevel)
            .Select(_ => Task.Run(() => _channel.Read(process, _cts.Token))).ToArray();
    }

    public ValueTask Write(T element, CancellationToken cancellationToken)
        => _disposing ? default : _channel.Write(element, false, cancellationToken);

    public ValueTask DisposeAsync() {
        if (_disposing) return default;

        _disposing = true;
        return _channel.Stop(_cts, _readerTasks);
    }

    bool _disposing;
}