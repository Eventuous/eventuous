// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

abstract class ChannelWorkerBase<T> : IAsyncDisposable {
    readonly CancellationTokenSource _cts      = new();
    readonly TaskCompletionSource    _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly Task[]                  _readerTasks;

    int _disposing;

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

    /// <summary>
    /// Idempotent. The commit handler worker is disposed by both the resubscribe and the shutdown
    /// paths, which can run concurrently, so a second call is expected rather than a programming
    /// error. It must not re-enter the shutdown: by then the CTS is disposed, and cancelling it
    /// again throws <see cref="ObjectDisposedException"/> out of host shutdown. The second caller
    /// awaits the first call's shutdown, so it can't return before the final checkpoint flush.
    /// </summary>
    public ValueTask DisposeAsync() => Interlocked.Exchange(ref _disposing, 1) == 0 ? new(StopWorker()) : new(_disposed.Task);

    async Task StopWorker() {
        try {
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
            _disposed.TrySetResult();
        } catch (Exception e) {
            // Don't let a waiter see a clean shutdown that didn't happen.
            _disposed.TrySetException(e);

            throw;
        }
    }
}
