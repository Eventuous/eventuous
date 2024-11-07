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
    [PublicAPI]
    public bool IsRunning { get; set; }

    [PublicAPI]
    public bool IsDropped { get; set; }

    protected internal T Options { get; }

    IEventSerializer                  EventSerializer { get; }
    internal  ConsumePipe             Pipe            { get; }
    protected ILoggerFactory?         LoggerFactory   { get; }
    protected LogContext              Log             { get; }
    protected CancellationTokenSource Stopping        { get; set; } = new();

    protected ulong Sequence;

    protected EventSubscription(
            T                    options,
            ConsumePipe          consumePipe,
            ILoggerFactory?      loggerFactory,
            IEventSerializer?    eventSerializer
        ) {
        Ensure.NotEmptyString(options.SubscriptionId);

        LoggerFactory   = loggerFactory;
        Pipe            = Ensure.NotNull(consumePipe);
        EventSerializer = eventSerializer ?? DefaultEventSerializer.Instance;
        Options         = options;
        Log             = Logger.CreateContext(options.SubscriptionId, loggerFactory);
    }

    OnSubscribed? _onSubscribed;
    OnDropped?    _onDropped;

    public string SubscriptionId => Options.SubscriptionId;

    public async ValueTask Subscribe(OnSubscribed onSubscribed, OnDropped onDropped, CancellationToken cancellationToken) {
        if (IsRunning) return;

        Stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _onSubscribed = onSubscribed;
        _onDropped    = onDropped;
        await Subscribe(Stopping.Token).NoContext();
        IsRunning = true;
        Log.SubscriptionStarted();
        onSubscribed(Options.SubscriptionId);
    }

    public async ValueTask Unsubscribe(OnUnsubscribed onUnsubscribed, CancellationToken cancellationToken) {
        IsRunning = false;
        await Unsubscribe(cancellationToken).NoContext();
        Log.SubscriptionStopped();
        onUnsubscribed(Options.SubscriptionId);
        await Finalize(cancellationToken);
        Sequence = 0;
        Stopping.Dispose();
    }

    protected virtual ValueTask Finalize(CancellationToken cancellationToken) => default;

    // ReSharper disable once CognitiveComplexity
    // ReSharper disable once CyclomaticComplexity
    protected async ValueTask Handler(IMessageConsumeContext context) {
        var scope = new Dictionary<string, object> {
            { "SubscriptionId", SubscriptionId },
            { "Stream", context.Stream },
            { "MessageType", context.MessageType },
        };

        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        Logger.Current ??= Log;

        using (Log.Logger.BeginScope(scope)) {
            var activity = EventuousDiagnostics.Enabled
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
            } catch (OperationCanceledException e) when (Stopping.IsCancellationRequested) {
                Log.MessageIgnoredWhenStopping(e);
            } catch (Exception e) { context.Nack(SubscriptionId, e); }

            if (context.HasFailed()) {
                if (activity != null) activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;

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

    // TODO: Passing the handler function would allow decoupling subscribers from handlers
    protected abstract ValueTask Subscribe(CancellationToken cancellationToken);

    protected abstract ValueTask Unsubscribe(CancellationToken cancellationToken);

    protected virtual async Task Resubscribe(TimeSpan delay, CancellationToken cancellationToken) {
        await Task.Delay(delay, cancellationToken).NoContext();

        while (IsRunning && IsDropped && !cancellationToken.IsCancellationRequested) {
            try {
                Log.SubscriptionResubscribing();

                await Subscribe(cancellationToken).NoContext();

                IsDropped = false;
                _onSubscribed?.Invoke(Options.SubscriptionId);

                Log.SubscriptionResubscribed();
            } catch (OperationCanceledException) { } catch (Exception e) {
                Log.SubscriptionResubscribeFailed(e);
                await Task.Delay(1000, cancellationToken).NoContext();
            }
        }
    }

    protected void Dropped(DropReason reason, Exception? exception) {
        if (!IsRunning) return;

        Log.SubscriptionDropped(reason, exception);

        IsDropped = true;
        _onDropped?.Invoke(Options.SubscriptionId, reason, exception);

        Task.Run(
            async () => {
                var delay = reason == DropReason.Stopped ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2);
                Log.SubscriptionWillResubscribe(delay);

                try { await Resubscribe(delay, Stopping.Token).NoContext(); } catch (Exception e) {
                    Log.WarnLog?.Log(e.Message);

                    throw;
                }
            }
        );
    }

    bool _disposed;

    public async ValueTask DisposeAsync() {
        if (_disposed) return;

        await Pipe.DisposeAsync().NoContext();

        // Stopping.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct EventPosition(ulong? Position, DateTime Created) {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventPosition FromContext(IMessageConsumeContext context) => new(context.StreamPosition, context.Created);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventPosition FromAllContext(IMessageConsumeContext context) => new(context.GlobalPosition, context.Created);
}
