using System.Runtime.CompilerServices;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.Logging;
using ActivityStatus = Eventuous.Diagnostics.ActivityStatus;

namespace Eventuous.Subscriptions;

[PublicAPI]
public abstract class EventSubscription<T> : IMessageSubscription where T : SubscriptionOptions {
    public    bool             IsRunning { get; set; }
    public    bool             IsDropped { get; set; }
    protected Logging.Logging? DebugLog  { get; }
    protected ILogger?         Log       { get; }

    protected internal T Options { get; }

    internal IEventSerializer EventSerializer { get; }
    internal IMessageConsumer Consumer        { get; }

    CancellationTokenSource _subscriptionCts = new();

    protected EventSubscription(
        T                options,
        IMessageConsumer consumer,
        ILoggerFactory?  loggerFactory = null
    ) {
        Ensure.NotEmptyString(options.SubscriptionId, options.SubscriptionId);

        Consumer        = Ensure.NotNull(consumer, nameof(consumer));
        EventSerializer = options.EventSerializer ?? DefaultEventSerializer.Instance;
        Options         = options;
        Log             = loggerFactory?.CreateLogger($"Subscription-{options.SubscriptionId}");
        DebugLog        = Log?.IsEnabled(LogLevel.Debug) == true ? Log.LogDebug : null;
    }

    OnSubscribed? _onSubscribed;
    OnDropped?    _onDropped;

    public string SubscriptionId => Options.SubscriptionId;

    public async ValueTask Subscribe(
        OnSubscribed      onSubscribed,
        OnDropped         onDropped,
        CancellationToken cancellationToken
    ) {
        _onSubscribed = onSubscribed;
        _onDropped    = onDropped;
        await Subscribe(cancellationToken);
        onSubscribed(Options.SubscriptionId);
    }

    public async ValueTask Unsubscribe(
        OnUnsubscribed    onUnsubscribed,
        CancellationToken cancellationToken
    ) {
        await Unsubscribe(cancellationToken);
        onUnsubscribed(Options.SubscriptionId);

        if (Consumer is IAsyncDisposable disposable) {
            await disposable.DisposeAsync();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected async ValueTask Handler(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        using var activity = SubscriptionActivity.Start(context);

        DebugLog?.Invoke(
            "Subscription {Subscription} got an event {EventType}",
            Options.SubscriptionId,
            context.MessageType
        );

        try {
            if (context.Message != null) {
                await Consumer.Consume(context, cancellationToken).NoContext();
            }
        }
        catch (Exception e) {
            activity?.SetStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));

            Log?.Log(
                Options.ThrowOnError ? LogLevel.Error : LogLevel.Warning,
                e,
                "Error when handling the event {Stream} {Type}",
                context.Stream,
                context.MessageType
            );

            if (Options.ThrowOnError)
                throw new SubscriptionException(
                    context.Stream,
                    context.MessageType,
                    context.Message,
                    e
                );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected object? DeserializeData(
        string               eventContentType,
        string               eventType,
        ReadOnlyMemory<byte> data,
        string               stream,
        ulong                position = 0
    ) {
        try {
            return EventSerializer.DeserializeSubscriptionPayload(
                eventContentType,
                eventType,
                data,
                stream,
                position
            );
        }
        catch (Exception e) {
            Log?.LogError(
                e,
                "Error deserializing event {Stream} {Position} {Type}",
                stream,
                position,
                eventType
            );

            if (Options.ThrowOnError) throw;

            return null;
        }
    }

    // TODO: Passing the handler function would allow decoupling subscribers from handlers
    protected abstract ValueTask Subscribe(CancellationToken cancellationToken);

    protected abstract ValueTask Unsubscribe(CancellationToken cancellationToken);

    readonly InterlockedSemaphore _resubscribing = new();

    protected virtual async Task Resubscribe(TimeSpan delay, CancellationToken cancellationToken) {
        if (_resubscribing.IsClosed()) return;

        Log?.LogWarning("Resubscribing");

        await Task.Delay(delay, cancellationToken).NoContext();

        while (IsRunning && IsDropped) {
            try {
                _resubscribing.Close();

                await Subscribe(cancellationToken).NoContext();

                IsDropped = false;
                _onSubscribed?.Invoke(Options.SubscriptionId);

                Log?.LogInformation("Subscription restored");
            }
            catch (Exception e) {
                Log?.LogError(e, "Unable to restart the subscription");

                await Task.Delay(1000, cancellationToken).NoContext();
            }
            finally {
                _resubscribing.Open();
            }
        }
    }

    protected void Dropped(DropReason reason, Exception? exception) {
        if (!IsRunning || _resubscribing.IsClosed()) return;

        Log?.LogWarning(exception, "Subscription dropped {Reason}", reason);

        IsDropped = true;
        _onDropped?.Invoke(Options.SubscriptionId, reason, exception);

        Task.Run(
            () => {
                var delay = reason == DropReason.Stopped ? TimeSpan.FromSeconds(10)
                    : TimeSpan.FromSeconds(2);

                return Resubscribe(delay, _subscriptionCts.Token);
            }
        );
    }
}

public record EventPosition(ulong? Position, DateTime Created) {
    public static EventPosition FromContext(MessageConsumeContext context)
        => new(context.StreamPosition, context.Created);

    public static EventPosition FromContext(IMessageConsumeContext context)
        => FromContext((context as MessageConsumeContext)!);
}