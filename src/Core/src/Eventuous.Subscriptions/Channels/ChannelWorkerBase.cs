// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

abstract class ChannelWorkerBase<T> : IAsyncDisposable {
    readonly CancellationTokenSource _cts = new();
    readonly Task[]                  _readerTasks;

    public Func<CancellationToken, ValueTask>? OnDispose { get; set; }

    public ValueTask Write(T element, CancellationToken cancellationToken)
        => _stopping ? default : _channel.Write(element, _throwOnFull, cancellationToken);

    bool                _stopping;
    readonly Channel<T> _channel;
    readonly bool       _throwOnFull;

    protected ChannelWorkerBase(Channel<T> channel, Func<CancellationToken, Task> processor, int concurrencyLevel, bool throwOnFull = false) {
        _channel     = channel;
        _throwOnFull = throwOnFull;
        _readerTasks = Enumerable.Range(0, concurrencyLevel).Select(_ => Task.Run(() => processor(_cts.Token))).ToArray();
    }

    public async ValueTask DisposeAsync() {
        _stopping = true;
        await _channel.Stop(_cts, _readerTasks, OnDispose).NoContext();
#if NET8_0_OR_GREATER
        await _cts.CancelAsync().NoContext();
#else
        _cts.Cancel();
#endif
        await Task.WhenAll(_readerTasks).NoThrow();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
