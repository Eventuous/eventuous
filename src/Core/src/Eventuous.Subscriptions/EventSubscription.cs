using System.Diagnostics;
using System.Runtime.CompilerServices;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions;

public abstract class EventSubscription<T> : IMessageSubscription where T : SubscriptionOptions {
    [PublicAPI]
    public bool IsRunning { get; set; }

    [PublicAPI]
    public bool IsDropped { get; set; }

    protected internal T Options { get; }

    IEventSerializer     EventSerializer { get; }
    internal ConsumePipe Pipe            { get; }

    protected CancellationTokenSource Stopping { get; } = new();

    protected EventSubscription(T options, ConsumePipe consumePipe) {
        Ensure.NotEmptyString(options.SubscriptionId);

        Pipe            = Ensure.NotNull(consumePipe);
        EventSerializer = options.EventSerializer ?? DefaultEventSerializer.Instance;
        Options         = options;
    }

    OnSubscribed? _onSubscribed;
    OnDropped?    _onDropped;

    public string SubscriptionId => Options.SubscriptionId;

    public async ValueTask Subscribe(
        OnSubscribed      onSubscribed,
        OnDropped         onDropped,
        CancellationToken cancellationToken
    ) {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Stopping.Token);

        _onSubscribed = onSubscribed;
        _onDropped    = onDropped;
        await Subscribe(cts.Token).NoContext();
        IsRunning = true;
        Log.SubscriptionStarted(Options.SubscriptionId);
        onSubscribed(Options.SubscriptionId);
    }

    public async ValueTask Unsubscribe(OnUnsubscribed onUnsubscribed, CancellationToken cancellationToken) {
        IsRunning = false;
        await Unsubscribe(cancellationToken).NoContext();
        Log.SubscriptionStopped(Options.SubscriptionId);
        onUnsubscribed(Options.SubscriptionId);
        await Pipe.DisposeAsync().NoContext();
        await Finalize(cancellationToken);
    }

    protected virtual ValueTask Finalize(CancellationToken cancellationToken) => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected async ValueTask Handler(IMessageConsumeContext context) {
        var activity = EventuousDiagnostics.Enabled
            ? SubscriptionActivity.Create(
                $"{Constants.Components.Subscription}.{SubscriptionId}/{context.MessageType}",
                ActivityKind.Internal,
                context,
                EventuousDiagnostics.Tags
            )
            : null;

        var delayed = context is DelayedAckConsumeContext;
        if (!delayed) activity?.Start();

        Log.MessageReceived(context);

        try {
            if (context.Message != null) {
                if (delayed && activity != null) {
                    activity.SetStartTime(DateTime.UtcNow);
                    context.Items.AddItem(ContextItemKeys.Activity, activity);
                }

                await Pipe.Send(context).NoContext();
            }
            else {
                context.Ignore(SubscriptionId);
            }

            if (context.WasIgnored() && activity != null) activity.ActivityTraceFlags = ActivityTraceFlags.None;
        }
        catch (Exception e) {
            context.Nack(SubscriptionId, e);
        }

        if (context.HasFailed()) {
            if (activity != null) activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            var exception = context.HandlingResults.GetException();

            if (Options.ThrowOnError) {
                activity?.Dispose();

                throw new SubscriptionException(
                    context.Stream,
                    context.MessageType,
                    context.Message,
                    exception ?? new InvalidOperationException()
                );
            }
        }

        if (!delayed) activity?.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected object? DeserializeData(
        string               eventContentType,
        string               eventType,
        ReadOnlyMemory<byte> data,
        string               stream,
        ulong                position = 0
    ) {
        if (data.IsEmpty) return null;

        var contentType = string.IsNullOrWhiteSpace(eventContentType) ? "application/json" : eventContentType;

        try {
            var result = EventSerializer.DeserializeEvent(data.Span, eventType, contentType);

            return result switch {
                SuccessfullyDeserialized success => success.Payload,
                FailedToDeserialize failed       => LogAndReturnNull(failed.Error),
                _                                => throw new ApplicationException($"Unknown result {result}")
            };
        }
        catch (Exception e) {
            var exception = new DeserializationException(stream, eventType, position, e);

            Log.PayloadDeserializationFailed(
                Options.SubscriptionId,
                stream,
                position,
                eventType,
                exception.ToString()
            );

            if (Options.ThrowOnError) throw;

            return null;
        }

        object? LogAndReturnNull(DeserializationError error) {
            Log.MessagePayloadInconclusive(SubscriptionId, eventType, stream, contentType, error);
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
                Log.SubscriptionResubscribing(Options.SubscriptionId);

                await Subscribe(cancellationToken).NoContext();

                IsDropped = false;
                _onSubscribed?.Invoke(Options.SubscriptionId);

                Log.SubscriptionRestored(Options.SubscriptionId);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) {
                Log.ResubscribeFailed(Options.SubscriptionId, e.Message);
                await Task.Delay(1000, cancellationToken).NoContext();
            }
        }
    }

    protected void Dropped(DropReason reason, Exception? exception) {
        if (!IsRunning) return;

        Log.SubscriptionDropped(Options.SubscriptionId, reason, exception);

        IsDropped = true;
        _onDropped?.Invoke(Options.SubscriptionId, reason, exception);

        Task.Run(
            async () => {
                var delay = reason == DropReason.Stopped
                    ? TimeSpan.FromSeconds(10)
                    : TimeSpan.FromSeconds(2);

                EventuousEventSource.Log.Warn($"Will resubscribe after {delay}");

                try {
                    await Resubscribe(delay, Stopping.Token);
                }
                catch (Exception e) {
                    EventuousEventSource.Log.Warn(e.Message);
                    throw;
                }
            }
        );
    }
}

public record EventPosition(ulong? Position, DateTime Created) {
    public static EventPosition FromContext(IMessageConsumeContext context)
        => new(context.Items.GetItem<ulong>(ContextKeys.StreamPosition), context.Created);
}
