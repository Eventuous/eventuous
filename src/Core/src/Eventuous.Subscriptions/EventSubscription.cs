using Eventuous.Subscriptions.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions;

[PublicAPI]
public abstract class EventSubscription<T> : IHostedService, IReportHealth where T : SubscriptionOptions {
    public    bool             IsRunning { get; set; }
    public    bool             IsDropped { get; set; }
    protected Logging.Logging? DebugLog  { get; }
    protected ILogger?         Log       { get; }

    protected internal T Options { get; }

    internal IEventSerializer         EventSerializer { get; }
    internal IEventHandler[]          EventHandlers   { get; }
    internal ISubscriptionGapMeasure? Measure         { get; }

    CancellationTokenSource? _cts;
    Task?                    _measureTask;
    EventPosition?           _lastProcessed;
    ulong                    _gap;

    public string ServiceId => Options.SubscriptionId;

    protected EventSubscription(
        T                          options,
        IEnumerable<IEventHandler> eventHandlers,
        ILoggerFactory?            loggerFactory = null,
        ISubscriptionGapMeasure?   measure       = null
    ) {
        Ensure.NotEmptyString(options.SubscriptionId, options.SubscriptionId);

        EventSerializer = options.EventSerializer ?? DefaultEventSerializer.Instance;
        Measure         = measure;
        Options         = options;
        EventHandlers   = Ensure.NotNull(eventHandlers, nameof(eventHandlers)).ToArray();
        Log             = loggerFactory?.CreateLogger($"Subscription-{options.SubscriptionId}");
        DebugLog        = Log?.IsEnabled(LogLevel.Debug) == true ? Log.LogDebug : null;
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        if (Measure != null) {
            _cts         = new CancellationTokenSource();
            _measureTask = Task.Run(() => MeasureGap(_cts.Token), _cts.Token);
        }

        await Subscribe(cancellationToken).NoContext();

        IsRunning = true;

        Log?.LogInformation("Started subscription");
    }

    protected virtual async Task Handler(ReceivedEvent re, CancellationToken cancellationToken) {
        DebugLog?.Invoke(
            "Subscription {Subscription} got an event {EventType}",
            Options.SubscriptionId,
            re.EventType
        );

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

            SetLastProcessedEventPosition(EventPosition.FromReceivedEvent(re));
        }
        catch (Exception e) {
            Log?.Log(
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
    }

    protected void SetLastProcessedEventPosition(EventPosition position) => _lastProcessed = position;

    protected EventPosition? GetLastProcessedEventPosition() => _lastProcessed;

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
            Log?.LogError(
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
            Log?.LogError(
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
    protected abstract Task Subscribe(CancellationToken cancellationToken);

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

        await Unsubscribe(cancellationToken).NoContext();

        Log?.LogInformation("Stopped subscription");
    }

    readonly InterlockedSemaphore _resubscribing = new();

    protected virtual async Task Resubscribe(TimeSpan delay) {
        if (_resubscribing.IsClosed()) return;

        Log?.LogWarning("Resubscribing");

        await Task.Delay(delay).NoContext();

        while (IsRunning && IsDropped) {
            try {
                _resubscribing.Close();

                await Subscribe(default).NoContext();

                IsDropped = false;

                Log?.LogInformation("Subscription restored");
            }
            catch (Exception e) {
                Log?.LogError(e, "Unable to restart the subscription");

                await Task.Delay(1000).NoContext();
            }
            finally {
                _resubscribing.Open();
            }
        }
    }

    protected void Dropped(DropReason reason, Exception? exception) {
        if (!IsRunning || _resubscribing.IsClosed()) return;

        Log?.LogWarning(exception, "Subscription dropped {Reason}", reason);

        IsDropped      = true;
        _lastException = exception;

        Task.Run(
            () => Resubscribe(reason == DropReason.Stopped ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2))
        );
    }

    protected abstract ValueTask Unsubscribe(CancellationToken cancellationToken);

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

public record EventPosition(ulong? Position, DateTime Created) {
    public static EventPosition FromReceivedEvent(ReceivedEvent receivedEvent)
        => new(receivedEvent.StreamPosition, receivedEvent.Created);
}