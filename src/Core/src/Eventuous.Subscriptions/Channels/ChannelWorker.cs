// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

class ChannelWorker<T> : IAsyncDisposable {
    readonly Channel<T>              _channel;
    readonly bool                    _throwOnFull;
    readonly CancellationTokenSource _cts;
    readonly Task                    _readerTask;

    public Func<CancellationToken, ValueTask>? OnDispose { get; set; }

    /// <summary>
    /// Creates a new instance of the channel worker, starts a task for background reads
    /// </summary>
    /// <param name="channel">Channel to use for writes and reads</param>
    /// <param name="process">Function to process each element the worker reads from the channel</param>
    /// <param name="throwOnFull">Throw if the channel is full to prevent partition blocks</param>
    public ChannelWorker(Channel<T> channel, ProcessElement<T> process, bool throwOnFull = false) {
        _channel     = channel;
        _throwOnFull = throwOnFull;
        _cts         = new CancellationTokenSource();
        _readerTask  = Task.Run(() => _channel.Read(process, _cts.Token));
    }

    public ValueTask Write(T element, CancellationToken cancellationToken)
        => _stopping ? default : _channel.Write(element, _throwOnFull, cancellationToken);
    
    bool _stopping;

    public async ValueTask DisposeAsync() {
        _stopping = true;
        await _channel.Stop(_cts, new[] { _readerTask }, OnDispose);
#if NET8_0
        await _cts.CancelAsync();
#else
        _cts.Cancel();
#endif
        await _readerTask.NoThrow();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
