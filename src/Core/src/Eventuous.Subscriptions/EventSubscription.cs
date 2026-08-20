// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using static Eventuous.DeserializationResult;

namespace Eventuous.Subscriptions;

using Context;
using Diagnostics;
using Filters;
using Logging;

public abstract class EventSubscription<T> : IMessageSubscription, IAsyncDisposable where T : SubscriptionOptions {
    protected internal T Options { get; }

    IEventSerializer          EventSerializer { get; }
    internal  ConsumePipe     Pipe            { get; }
    protected ILoggerFactory? LoggerFactory   { get; }
    protected LogContext      Log             { get; }

    Session? _session;
    int      _disposed;

    [PublicAPI]
    public bool IsRunning => Volatile.Read(ref _session) is not null;

    protected EventSubscription(
            T                    options,
            ConsumePipe          consumePipe,
            ILoggerFactory?      loggerFactory,
            IEventSerializer?    eventSerializer
        ) {
        Ensure.NotEmptyString(options.SubscriptionId);

        LoggerFactory   = loggerFactory;
        Pipe            = Ensure.NotNull(consumePipe);
        EventSerializer = eventSerializer ?? Eventuous.EventSerializer.Default;
        Options         = options;
        Log             = Logger.CreateContext(options.SubscriptionId, loggerFactory);
    }

    public string SubscriptionId => Options.SubscriptionId;

    public async ValueTask Subscribe(OnSubscribed onSubscribed, OnDropped onDropped, CancellationToken cancellationToken) {
        // Otherwise a new run could deliver into a pipe that's already disposed.
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var lifetime    = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var finishedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var settings    = SupervisorSettings.From(Options, Log);
        var session     = new Session(lifetime, finishedTcs, onSubscribed, onDropped, settings);

        // Refused, not silently ignored: serving a second caller would take the run away from the first
        // without telling it.
        if (Interlocked.CompareExchange(ref _session, session, comparand: null) is not null) {
            lifetime.Dispose();
            throw new InvalidOperationException($"Subscription {SubscriptionId} is already running. Unsubscribe before subscribing again.");
        }

        // Rechecked after publishing, because the check at the top races DisposeAsync: publish-then-check here
        // against set-then-read there guarantees one side observes the other — either the disposal finds this
        // session and stops it, or this read finds _disposed set and unwinds. The top check alone lets a
        // subscribe slip past a concurrent disposal into a disposed pipe, with nothing left to stop it.
        if (Volatile.Read(ref _disposed) != 0) {
            Interlocked.CompareExchange(ref _session, null, session);
            finishedTcs.TrySetResult();
            lifetime.Dispose();

            throw new ObjectDisposedException(GetType().FullName);
        }

        // Guarded because CreateRun can throw: a session published with no supervisor to clear it is an
        // unstoppable subscription whose DisposeAsync never returns.
        SubscriptionRun? run = null;

        try {
            run = CreateRun(lifetime.Token);
            await Connect(run).NoContext();
        } catch {
            if (run is not null) {
                using var graceful = GracefulStop(settings);
                await run.Stop(graceful.Token, Log).NoContext();
            }

            // Cleared before rethrowing so an immediate retry isn't refused.
            Interlocked.CompareExchange(ref _session, null, session);
            finishedTcs.TrySetResult();
            lifetime.Dispose();

            throw;
        }

        Log.SubscriptionStarted();
        ReportConnected(session);

        _ = Task.Run(() => RunSubscriptionLoop(session, run), CancellationToken.None);
    }

    public async ValueTask Unsubscribe(OnUnsubscribed onUnsubscribed, CancellationToken cancellationToken) {
        await StopSession(cancellationToken).NoContext();

        Log.SubscriptionStopped();
        onUnsubscribed(SubscriptionId);
    }

    /// <summary>
    /// Cancels the running session, if any, and waits for its supervisor to finish.
    /// <paramref name="cancellationToken"/> bounds only this wait — teardown has its own budget.
    /// </summary>
    async ValueTask StopSession(CancellationToken cancellationToken) {
        if (Volatile.Read(ref _session) is not { } session) return;

        // Guarded: cancelling runs whatever the transport registered on the token, and its failure
        // shouldn't cost the caller its stop report.
        try {
            await session.Lifetime.CancelAsync().NoContext();
        } catch (ObjectDisposedException) {
            // Already disposed means the supervisor already finished; nothing left to cancel.
        } catch (Exception e) {
            Log.SubscriptionDisconnectFailed(e);
        }

        try {
            await session.Finished.Task.WaitAsync(cancellationToken).NoContext();
        } catch (OperationCanceledException) {
            // Logged, not thrown: this runs on the host's shutdown token from IHostedService.StopAsync,
            // and throwing would abort every service queued behind it.
            Log.SubscriptionStopTimedOut();
        }

        // Discarded even on timeout, or a teardown that outlived the caller would refuse every later Subscribe.
        Interlocked.CompareExchange(ref _session, null, session);
    }

    /// <summary>
    /// The subscription's lifecycle from the first successful connect onward, sequential and single-threaded.
    /// <paramref name="run"/> arrives already connected, so every failure here is a drop to report and
    /// recover from, never a caller still waiting on the first attempt.
    /// </summary>
    async Task RunSubscriptionLoop(Session session, SubscriptionRun run) {
        var lifetime = session.Lifetime.Token;
        var settings = session.Settings;

        try {
            while (true) {
                // Fires on a reported drop, a dying pump, or shutdown cancelling the run's token.
                await run.Ended.NoContext();

                // Skipped during shutdown: a transport whose client reacts to token cancellation would
                // double-report otherwise; Fail is first-wins, so this only fires for a genuine failure.
                if (run.Failure is { } failure && !lifetime.IsCancellationRequested) ReportConnectionDropped(session, failure);

                // Same teardown whether replaced or final — a stop is a resubscribe that doesn't come back.
                using (var graceful = GracefulStop(settings)) {
                    await run.Stop(graceful.Token, Log).NoContext();
                }

                if (lifetime.IsCancellationRequested) break;

                Log.SubscriptionWillResubscribe(settings.RetryDelay);

                try {
                    await Task.Delay(settings.RetryDelay, lifetime).NoContext();
                } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
                    break;
                }

                Log.SubscriptionResubscribing();

                // Outside the try: failing here is the supervisor's fault, not a transport drop, and there's no
                // live run to carry it — blaming the one just released would re-report its stale failure.
                run = CreateRun(lifetime);

                try {
                    await Connect(run).NoContext();
                    Log.SubscriptionResubscribed();
                    ReportConnected(session);
                } catch (Exception e) {
                    // Handled by the top of the loop, same path as a mid-run drop.
                    run.Fail(DropReason.ServerError, e);
                }
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
            // Shutdown landed mid-run or mid-delay.
        } catch (Exception e) {
            Log.SubscriptionSuperviseFailed(e);

            // Only chance to report: health is wired to these two callbacks, so dying silently here would
            // leave health green. SubscriptionError since this is the supervisor's failure, not the transport's.
            if (!lifetime.IsCancellationRequested) ReportConnectionDropped(session, new(DropReason.SubscriptionError, e));
        } finally {
            Interlocked.CompareExchange(ref _session, null, session);
            session.Finished.TrySetResult();

            // Unregisters the session from the caller's long-lived token; otherwise each subscribe cycle
            // leaks a registration the GC can't reclaim. An Unsubscribe that races this finds the source
            // already disposed, which is the answer it wants.
            session.Lifetime.Dispose();
        }
    }

    void ReportConnected(Session session) {
        try { session.OnSubscribed(SubscriptionId); } catch (Exception e) { Log.SubscriptionCallbackFailed(e); }
    }

    void ReportConnectionDropped(Session session, Failure failure) {
        Log.SubscriptionDropped(failure.Reason, failure.Exception);

        try { session.OnDropped(SubscriptionId, failure.Reason, failure.Exception); } catch (Exception e) { Log.SubscriptionCallbackFailed(e); }
    }

    /// <summary>
    /// The budget a run gets to stop itself in, on the one path that has no caller waiting to supply one.
    /// </summary>
    CancellationTokenSource GracefulStop(SupervisorSettings settings) => new(settings.TeardownTimeout);

    /// <summary>
    /// Creates the run for one attempt. Override to attach attempt-scoped state a base run field can't hold.
    /// </summary>
    protected virtual SubscriptionRun CreateRun(CancellationToken lifetime) => new(lifetime);

    // ReSharper disable once CognitiveComplexity
    // ReSharper disable once CyclomaticComplexity
    protected async ValueTask Handler(IMessageConsumeContext context) {
        // Use KeyValuePair array instead of Dictionary for 5x speedup and 3x less allocation
        var scope = new KeyValuePair<string, object>[] {
            new("SubscriptionId", SubscriptionId),
            new("Stream", context.Stream),
            new("MessageType", context.MessageType)
        };

        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        Logger.Current ??= Log;

        using (Log.Logger.BeginScope(scope)) {
            // No activity for payload-less contexts: they are ignored and acknowledged below without
            // entering the pipe, so an activity would never be started or disposed on the async path —
            // a pure allocation leak, hot since checkpoint-reached contexts arrive payload-less.
            var activity = EventuousDiagnostics.Enabled && context.Message != null
                ? SubscriptionActivity.Create(
                    $"{Constants.Components.Subscription}.{SubscriptionId}/{context.MessageType}",
                    ActivityKind.Internal,
                    context,
                    EventuousDiagnostics.Tags
                )
                : null;

            var isAsync = context is AsyncConsumeContext;
            if (!isAsync) activity?.Start();

            Log.MessageReceived(context);

            try {
                if (context.Message != null) {
                    if (activity != null) {
                        context.ParentContext = activity.Context;

                        if (isAsync) { context.Items.AddItem(ContextItemKeys.Activity, activity); }
                    }

                    await Pipe.Send(context).NoContext();
                }
                else {
                    context.Ignore(SubscriptionId);

                    if (isAsync) {
                        var asyncContext = context as AsyncConsumeContext;
                        await asyncContext!.Acknowledge().NoContext();
                    }
                }

                if (context.WasIgnored() && activity != null) activity.ActivityTraceFlags = ActivityTraceFlags.None;
            } catch (OperationCanceledException e) when (context.CancellationToken.IsCancellationRequested) {
                Log.MessageIgnoredWhenStopping(e);
            } catch (Exception e) { context.Nack(SubscriptionId, e); }

            if (context.HasFailed()) {
                activity?.ActivityTraceFlags = ActivityTraceFlags.Recorded;

                var exception = context.HandlingResults.GetException();

                if (Options.ThrowOnError) {
                    activity?.Dispose();

                    throw new SubscriptionException(context.Stream, context.MessageType, context.Message, exception ?? new InvalidOperationException());
                }
            }

            if (!isAsync) activity?.Dispose();
        }
    }

    protected object? DeserializeData(string eventContentType, string eventType, ReadOnlyMemory<byte> data, string stream, ulong position = 0) {
        if (data.IsEmpty) return null;

        var contentType = string.IsNullOrWhiteSpace(eventContentType) ? "application/json" : eventContentType;

        try {
            var result = EventSerializer.DeserializeEvent(data.Span, eventType, contentType);

            return result switch {
                SuccessfullyDeserialized success => success.Payload,
                FailedToDeserialize failed       => LogAndReturnNull(failed.Error),
                _                                => throw new ApplicationException($"Unknown result {result}")
            };
        } catch (Exception e) {
            var exception = new DeserializationException(stream, eventType, position, e);
            Log.PayloadDeserializationFailed(stream, position, eventType, exception);

            if (Options.ThrowOnError) throw;

            return null;
        }

        object? LogAndReturnNull(DeserializationError error) {
            Log.MessagePayloadInconclusive(eventType, stream, error);

            return null;
        }
    }

    /// <summary>
    /// Connects the transport. Returns once up, throws if it can't come up. Called once per run and must be
    /// repeatable on the same instance — reassign fields rather than assume them unset.
    /// </summary>
    /// <remarks>
    /// A transport with its own polling or reading loop starts it here on a task of its own, reports the
    /// loop's death as this run's failure (unless <see cref="SubscriptionRun.Token"/> itself ended it), and
    /// registers an <see cref="SubscriptionRun.OnDisconnect"/> callback that awaits the loop so the next
    /// Connect never overlaps it. A callback-driven transport just connects and returns. Register each
    /// acquired handle on <paramref name="run"/> via <see cref="SubscriptionRun.OnDisconnect"/> as it's
    /// acquired, so a Connect that fails part-way still releases what was taken.
    /// </remarks>
    protected abstract ValueTask Connect(SubscriptionRun run);

    public async ValueTask DisposeAsync() {
        // Exchange, not check-then-set: prevents two concurrent disposals both reaching the pipe (disposing
        // it twice double-disposes every filter).
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // Before the pipe, since a live run delivers into it. Unbounded wait is safe because teardown
        // carries its own budget.
        await StopSession(CancellationToken.None).NoContext();

        await Pipe.DisposeAsync().NoContext();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Everything one <see cref="Subscribe"/> call brought: the run token, stop signal, callbacks, and
    /// settings. One record, so <c>Unsubscribe</c> reads a consistent set rather than independently-moving
    /// fields.
    /// </summary>
    sealed record Session(CancellationTokenSource Lifetime, TaskCompletionSource Finished, OnSubscribed OnSubscribed, OnDropped OnDropped, SupervisorSettings Settings);
}

/// <summary>
/// <see cref="SubscriptionOptions"/> validated once, at <see cref="EventSubscription{T}.Subscribe"/>, rather
/// than on every use, since options are mutable and an operator should hear about a bad setting once per
/// subscribe, not once per reconnect.
/// </summary>
internal readonly record struct SupervisorSettings(TimeSpan RetryDelay, TimeSpan TeardownTimeout) {
    /// <summary>
    /// The longest finite wait <see cref="Task.Delay(TimeSpan, CancellationToken)"/> and
    /// <see cref="CancellationTokenSource(TimeSpan)"/> accept, about 49 days. Both throw above it, and a throw
    /// from the supervisor's delay or its graceful stop ends the loop for good.
    /// </summary>
    static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    public static SupervisorSettings From(SubscriptionOptions options, LogContext log) {
        var retryDelay = options.RetryDelay;

        if (!CanBeWaitedOn(retryDelay)) {
            log.SubscriptionRetryDelayInvalid(retryDelay, SubscriptionOptions.DefaultRetryDelay);
            retryDelay = SubscriptionOptions.DefaultRetryDelay;
        }

        var teardownTimeout = options.TeardownTimeout;

        if (!CanBeWaitedOn(teardownTimeout)) {
            log.SubscriptionTeardownTimeoutInvalid(teardownTimeout, SubscriptionOptions.DefaultTeardownTimeout);
            teardownTimeout = SubscriptionOptions.DefaultTeardownTimeout;
        }

        return new(retryDelay, teardownTimeout);
    }

    // InfiniteTimeSpan is exempt: both Task.Delay and CancellationTokenSource accept it as "never". Every other
    // out-of-range value is a configuration mistake that would otherwise surface as an exception thrown deep in
    // the supervisor, where the only available answer is to give up on the subscription.
    static bool CanBeWaitedOn(TimeSpan delay) => delay == Timeout.InfiniteTimeSpan || (delay >= TimeSpan.Zero && delay <= MaxDelay);
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct EventPosition(ulong? Position, DateTime Created) {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventPosition FromContext(IMessageConsumeContext context) => new(context.StreamPosition, context.Created);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventPosition FromAllContext(IMessageConsumeContext context) => new(context.GlobalPosition, context.Created);
}
