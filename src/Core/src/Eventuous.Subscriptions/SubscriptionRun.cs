// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions;

using Logging;

/// <summary>
/// Why a run ended. A reference type so <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/> can make
/// "first reason wins" a single atomic operation instead of a lock.
/// </summary>
sealed record Failure(DropReason Reason, Exception? Exception);

/// <summary>
/// One attempt at being subscribed. State scoped to that attempt — token, sequence, failure — lives here so
/// a late signal names the run it belongs to, not whichever run happens to be current.
/// </summary>
/// <remarks>
/// Derivable, so a subscription can hang per-attempt state off the run and release it via
/// <see cref="OnDisconnect"/> instead of a field the next run would overwrite. <see cref="Stop"/> owns its
/// own teardown order.
/// </remarks>
public class SubscriptionRun {
    readonly CancellationTokenSource _cts;

    // Load-bearing, not hygiene: without it, Fail from the channel worker resumes the supervisor inline and
    // runs the whole teardown on the thread that owns the message reader.
    readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    Failure?                                  _failure;
    ulong                                     _sequence;
    List<Func<CancellationToken, ValueTask>>? _releases;

    // protected: a transport can derive its own run. internal: the supervisor builds the plain one, which
    // isn't itself a SubscriptionRun.
    protected internal SubscriptionRun(CancellationToken lifetime) {
        _cts  = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        Token = _cts.Token; // Copied while the source is alive, so it stays readable after disposal.

        // Also completes Ended, so shutdown and failure arrive through one arm.
        _cts.Token.Register(static s => ((TaskCompletionSource)s!).TrySetResult(), _ended);
    }

    /// <summary>
    /// Cancelled when this run ends. The token a transport gives its I/O.
    /// </summary>
    public CancellationToken Token { get; }

    /// <summary>
    /// Completed when this run is over, for any reason — covers both a failure and a shutdown.
    /// </summary>
    public Task Ended => _ended.Task;

    /// <summary>
    /// Returns the value from before the increment, like the <c>Sequence++</c> it replaces. Belongs to the
    /// run, so a replacement starts from zero by construction.
    /// </summary>
    public ulong NextSequence() => Interlocked.Increment(ref _sequence) - 1;

    /// <summary>
    /// Ends this run. Safe from any thread, any run age, inside a catch or finally. Touches nothing disposable,
    /// logs and invokes nothing — the supervisor reports the drop, once per run, on its own stack.
    /// </summary>
    public void Fail(DropReason reason, Exception? exception) {
        if (Interlocked.CompareExchange(ref _failure, new(reason, exception), null) is not null) return;

        _ended.TrySetResult();
    }

    internal Failure? Failure => Volatile.Read(ref _failure);

    /// <summary>
    /// Stops this run: production stops, the run ends, then registered handles release in reverse
    /// registration order — first registered releases last, since acks in flight must land before the
    /// handles they need are gone.
    /// </summary>
    /// <remarks>
    /// Returns only once every release has finished. <paramref name="graceful"/> tells them to hurry, it
    /// does not cut them off — see <see cref="Disconnect"/>. Never throws; runs once per run.
    /// </remarks>
    internal async ValueTask Stop(CancellationToken graceful, LogContext log) {
        // Also completes Ended (see ctor). Guarded because it also runs whatever the transport registered
        // on this token.
        try {
            await _cts.CancelAsync().NoContext();
        } catch (Exception e) {
            log.SubscriptionDisconnectFailed(e);
        }

        try {
            await Disconnect(graceful, log).NoContext();
        } finally {
            // Must run even if Disconnect throws (it doesn't today): unregisters this run from the
            // subscription's lifetime token, which outlives it.
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Registers a handle and its release. Call from Connect as each handle is acquired, so a Connect that
    /// throws part-way still has everything taken so far released by teardown. Releases run in reverse
    /// acquisition order, after the token cancels.
    /// </summary>
    /// <remarks>
    /// Called only from Connect, on the supervisor's stack — the same stack every release runs on later — so
    /// no lock is needed.
    /// </remarks>
    public void OnDisconnect(Func<CancellationToken, ValueTask> release) {
        ArgumentNullException.ThrowIfNull(release);
        (_releases ??= []).Add(release);
    }

    /// <summary>
    /// Releases every handle registered through <see cref="OnDisconnect"/>, in reverse order, each guarded so
    /// one failure can't strand the rest. Never throws; clears registrations so a second call is a no-op.
    /// </summary>
    /// <remarks>
    /// <paramref name="graceful"/> means "stop being graceful and finish quickly", never "stop": every release
    /// is awaited, or the next run could read a checkpoint the previous one hadn't finished writing.
    /// </remarks>
    async ValueTask Disconnect(CancellationToken graceful, LogContext log) {
        if (_releases is not { Count: > 0 } releases) return;

        for (var i = releases.Count - 1; i >= 0; i--) {
            // Invocation inside the try: synchronous release bodies throw before returning a ValueTask.
            try {
                await releases[i](graceful).NoContext();
            } catch (Exception e) {
                log.SubscriptionDisconnectFailed(e);
            }
        }

        releases.Clear();
    }
}
