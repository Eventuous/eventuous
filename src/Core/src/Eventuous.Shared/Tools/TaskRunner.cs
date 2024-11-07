// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Tools;

public sealed class TaskRunner(Func<CancellationToken, Task> taskFactory) : IDisposable {
    readonly CancellationTokenSource _stopSource = new();

    Task? _runner;

    public TaskRunner Start() {
        _runner = Task.Run(Run);

        return this;

        async Task Run() => await taskFactory(_stopSource.Token).NoThrow();
    }

    /// <summary>
    /// Stops the running task, considering the cancellation token provided as an argument.
    /// The code of this function closely resembles BackgroundService.StopAsync function.
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async ValueTask Stop(CancellationToken cancellationToken) {
        if (_runner == null) return;

        try {
#if NET8_0_OR_GREATER
            await _stopSource.CancelAsync();
#else
            _stopSource.Cancel();
#endif
        } finally {
            var state        = new TaskCompletionSource<object>();
            var registration = cancellationToken.Register((s => (((TaskCompletionSource<object>)s!)).SetCanceled(cancellationToken)), state);

            try {
                await Task.WhenAny(_runner, state.Task).NoContext();
            } finally {
                await registration.DisposeAsync();
            }

            // ReSharper disable once RedundantAssignment
            registration = new();
            _runner      = null;
        }
    }

    public void Dispose() {
        _stopSource.Dispose();
        _runner?.Dispose();
    }
}
