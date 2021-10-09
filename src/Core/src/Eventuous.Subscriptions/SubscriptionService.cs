using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Subscriptions.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions;

[PublicAPI]
public abstract class SubscriptionService<T> : IHostedService, IReportHealth where T : SubscriptionOptions {
    internal  bool              IsRunning    { get; set; }
    internal  bool              IsDropped    { get; set; }
    protected EventSubscription Subscription { get; set; } = null!;
    protected SubscriptionLog   Log          { get; }

    protected internal T Options { get; }

    internal ICheckpointStore         CheckpointStore { get; }
    internal IEventSerializer         EventSerializer { get; }
    internal IEventHandler[]          EventHandlers   { get; }
    internal ISubscriptionGapMeasure? Measure         { get; }

    CancellationTokenSource? _cts;
    Task?                    _measureTask;
    EventPosition?           _lastProcessed;
    ulong                    _gap;

    public string ServiceId => Options.SubscriptionId;

    protected SubscriptionService(
        T                          options,
        ICheckpointStore           checkpointStore,
        IEnumerable<IEventHandler> eventHandlers,
        ILoggerFactory?            loggerFactory = null,
        ISubscriptionGapMeasure?   measure       = null
    ) {
        CheckpointStore = Ensure.NotNull(checkpointStore, nameof(checkpointStore));
        EventSerializer = options.EventSerializer ?? DefaultEventSerializer.Instance;
        Measure         = measure;

        Ensure.NotEmptyString(options.SubscriptionId, options.SubscriptionId);
        Options = options;

        EventHandlers = Ensure.NotNull(eventHandlers, nameof(eventHandlers)).ToArray();

        Log = new SubscriptionLog(loggerFactory, options.SubscriptionId);
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        if (EventHandlers.Length == 0) {
            Log.Warn("No handlers provided, subscription won't start");
            return;
        }
        
        if (Measure != null) {
            _cts         = new CancellationTokenSource();
            _measureTask = Task.Run(() => MeasureGap(_cts.Token), _cts.Token);
        }

        var checkpoint = await CheckpointStore
            .GetLastCheckpoint(Options.SubscriptionId, cancellationToken)
            .NoContext();

        _lastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

        Subscription = await Subscribe(checkpoint, cancellationToken).NoContext();

        IsRunning = true;

        Log.Info(
            "Handlers: {Handlers}",
            string.Join(",", EventHandlers.Select(x => x.GetType().Name))
        );

        Log.Info("Started subscription");
    }

    protected async Task Handler(ReceivedEvent re, CancellationToken cancellationToken) {
        Log.Debug?.Invoke(
            "Subscription {Subscription} got an event {EventType}",
            Options.SubscriptionId,
            re.EventType
        );

        // TODO: This is ESDB-specific and must be moved elsewhere
        if (re.EventType.StartsWith("$")) {
            _lastProcessed = GetPosition(re);
            await Store().NoContext();
            return;
        }

        try {
            if (re.Payload != null) {
                await Task.WhenAll(
                        EventHandlers.Select(
                            x => x.HandleEvent(
                                re,
                                cancellationToken
                            )
                        )
                    )
                    .NoContext();
            }

            _lastProcessed = GetPosition(re);
        }
        catch (Exception e) {
            Log.Log(
                Options.ThrowOnError ? LogLevel.Error : LogLevel.Warning,
                e,
                "Error when handling the event {Stream} {Position} {Type}",
                re.Stream,
                re.StreamPosition,
                re.EventType
            );

            if (Options.ThrowOnError)
                throw new SubscriptionException(
                    re.Stream,
                    re.EventType,
                    re.Sequence,
                    re.Payload,
                    e
                );
        }

        await Store().NoContext();

        Task Store() => StoreCheckpoint(GetPosition(re), cancellationToken);

        static EventPosition GetPosition(ReceivedEvent receivedEvent)
            => new(receivedEvent.StreamPosition, receivedEvent.Created);
    }

    protected async Task StoreCheckpoint(
        EventPosition     position,
        CancellationToken cancellationToken
    ) {
        _lastProcessed = position;
        var checkpoint = new Checkpoint(Options.SubscriptionId, position.Position);

        await CheckpointStore.StoreCheckpoint(checkpoint, cancellationToken).NoContext();
    }

    protected object? DeserializeData(
        string               eventContentType,
        string               eventType,
        ReadOnlyMemory<byte> data,
        string               stream,
        ulong                position = 0
    ) {
        if (data.IsEmpty) return null;

        var contentType = string.IsNullOrWhiteSpace(eventType) ? "application/json"
            : eventContentType;

        if (contentType != EventSerializer.ContentType) {
            Log.Error(
                "Unknown content type {ContentType} for event {Stream} {Position} {Type}",
                contentType,
                stream,
                position,
                eventType
            );

            if (Options.ThrowOnError)
                throw new InvalidOperationException($"Unknown content type {contentType}");
        }

        try {
            return EventSerializer.DeserializeEvent(data.Span, eventType);
        }
        catch (Exception e) {
            Log.Error(
                e,
                "Error deserializing event {Stream} {Position} {Type}",
                stream,
                position,
                eventType
            );

            if (Options.ThrowOnError)
                throw new DeserializationException(stream, eventType, position, e);

            return null;
        }
    }

    // TODO: Passing the handler function would allow decoupling subscribers from handlers
    protected abstract Task<EventSubscription> Subscribe(
        Checkpoint        checkpoint,
        CancellationToken cancellationToken
    );

    public async Task StopAsync(CancellationToken cancellationToken) {
        IsRunning = false;

        if (_measureTask != null) {
            _cts?.Cancel();

            try {
                await _measureTask.NoContext();
            }
            catch (OperationCanceledException) {
                // Expected
            }
        }

        await Subscription.Stop(cancellationToken).NoContext();

        Log.Info("Stopped subscription");
    }

    readonly InterlockedSemaphore _resubscribing = new();

    protected async Task Resubscribe(TimeSpan delay) {
        if (_resubscribing.IsClosed()) return;

        Log.Warn("Resubscribing");

        await Task.Delay(delay).NoContext();

        while (IsRunning && IsDropped) {
            try {
                _resubscribing.Close();

                var checkpoint = new Checkpoint(Options.SubscriptionId, _lastProcessed?.Position);

                Subscription = await Subscribe(checkpoint, default).NoContext();

                IsDropped = false;

                Log.Info("Subscription restored");
            }
            catch (Exception e) {
                Log.Error(e, "Unable to restart the subscription");

                await Task.Delay(1000).NoContext();
            }
            finally {
                _resubscribing.Open();
            }
        }
    }

    protected void Dropped(DropReason reason, Exception? exception) {
        if (!IsRunning || _resubscribing.IsClosed()) return;

        Log.Warn(exception, "Subscription dropped {Reason}", reason);

        IsDropped      = true;
        _lastException = exception;

        Task.Run(
            () => Resubscribe(reason == DropReason.Stopped ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2))
        );
    }

    async Task MeasureGap(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            var (position, created) = await GetLastEventPosition(cancellationToken).NoContext();

            if (_lastProcessed?.Position != null && position != null) {
                _gap = (ulong)position - _lastProcessed.Position.Value;

                Measure!.PutGap(Options.SubscriptionId, _gap, created);
            }

            await Task.Delay(1000, cancellationToken).NoContext();
        }
    }

    protected abstract Task<EventPosition> GetLastEventPosition(
        CancellationToken cancellationToken
    );

    Exception? _lastException;

    public HealthReport HealthReport => new(!(IsRunning && IsDropped), _lastException);
}

public record EventPosition(ulong? Position, DateTime Created);