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

    volatile bool       _stopping;
    readonly Channel<T> _channel;

    protected ChannelWorkerBase(Channel<T> channel, Func<CancellationToken, Task> processor, int concurrencyLevel) {
        _channel     = channel;
        _readerTasks = [.. Enumerable.Range(0, concurrencyLevel).Select(_ => Task.Run(() => processor(_cts.Token)))];
    }

    /// <summary>
    /// Queues an element and reports whether the worker took it. A stopping worker takes nothing — the
    /// caller must not count a refused element as processed.
    /// </summary>
    public async ValueTask<bool> Write(T element, CancellationToken cancellationToken) {
        if (_stopping) return false;

        try {
            await _channel.Writer.WriteAsync(element, cancellationToken).NoContext();

            return true;
        } catch (ChannelClosedException) {
            // The flag is set just before the channel completes, so a writer that got past it can still
            // find it closed — same event, caught here rather than propagated.
            return false;
        }
    }

    /// <summary>
    /// Idempotent: a worker can outlive the thing disposing it (the handling filter's worker is released
    /// with the pipe, a commit handler's with its run), so a second call is expected, not a bug. Must not
    /// re-enter shutdown — cancelling an already-disposed CTS throws <see cref="ObjectDisposedException"/>
    /// out of host shutdown. Every caller awaits the same task, so a failed shutdown is reported to all of
    /// them rather than left unobserved.
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
                // Runs even if the graceful stop failed: readers hold _cts.Token (Stop armed a ten-second
                // timer on it) and outlive the worker unless cancelled here. Cancelling runs their
                // callbacks, so this can't be allowed to throw.
                await _cts.CancelAsync().NoThrow();
                await Task.WhenAll(_readerTasks).NoThrow();
                _cts.Dispose();
                GC.SuppressFinalize(this);
            }

            _disposed.TrySetResult();
        } catch (Exception e) {
            // Broad on purpose: every caller awaits _disposed.Task, so an escaping exception here would
            // strand all of them.
            _disposed.TrySetException(e);
        }
    }
}
