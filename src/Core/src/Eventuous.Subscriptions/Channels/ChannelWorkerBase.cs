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
    /// again throws <see cref="ObjectDisposedException"/> out of host shutdown. Every caller awaits
    /// the same task, so none of them returns before the final checkpoint flush, and a shutdown that
    /// failed is reported to whoever awaits it instead of being left on a task nobody observes.
    /// </summary>
    public ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposing, 1) == 0) _ = StopWorker();

        return new(_disposed.Task);
    }

    async Task StopWorker() {
        try {
            try {
                _stopping = true;
                await _channel.Stop(_cts, _readerTasks, OnDispose).NoContext();
            }
            finally {
                // Release the readers even when the graceful stop above failed: they hold _cts.Token,
                // and Stop armed a ten-second timer on it, so both outlive the worker unless cancelled
                // here. Cancelling runs their callbacks, which is why this can't be allowed to throw.
                await _cts.CancelAsync().NoThrow();
                await Task.WhenAll(_readerTasks).NoThrow();
                _cts.Dispose();
                GC.SuppressFinalize(this);
            }

            _disposed.TrySetResult();
        } catch (Exception e) { _disposed.TrySetException(e); }
    }
}
