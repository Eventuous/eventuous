using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions {
    [PublicAPI]
    public abstract class SubscriptionService : IHostedService, IReportHealth {
        internal  bool              IsRunning      { get; set; }
        internal  bool              IsDropped      { get; set; }
        public    string            SubscriptionId { get; }
        protected EventSubscription Subscription   { get; set; } = null!;
        protected Logging?          DebugLog       { get; }
        protected ILogger?          Log            { get; }

        readonly ICheckpointStore         _checkpointStore;
        readonly IEventSerializer         _eventSerializer;
        readonly IEventHandler[]          _eventHandlers;
        readonly ISubscriptionGapMeasure? _measure;
        readonly bool                     _throwOnError;

        CancellationTokenSource? _cts;
        Task?                    _measureTask;
        EventPosition?           _lastProcessed;
        ulong                    _gap;

        protected SubscriptionService(
            SubscriptionOptions        options,
            ICheckpointStore           checkpointStore,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer?          eventSerializer = null,
            ILoggerFactory?            loggerFactory   = null,
            ISubscriptionGapMeasure?   measure         = null
        ) {
            _checkpointStore = Ensure.NotNull(checkpointStore, nameof(checkpointStore));
            _eventSerializer = eventSerializer ?? DefaultEventSerializer.Instance;
            SubscriptionId   = Ensure.NotEmptyString(options.SubscriptionId, options.SubscriptionId);
            _measure         = measure;
            _throwOnError    = options.ThrowOnError;

            _eventHandlers = Ensure.NotNull(eventHandlers, nameof(eventHandlers))
                .Where(x => x.SubscriptionId == options.SubscriptionId)
                .ToArray();

            Log = loggerFactory?.CreateLogger($"StreamSubscription-{options.SubscriptionId}");

            DebugLog = Log?.IsEnabled(LogLevel.Debug) == true ? Log.LogDebug : null;
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            if (_measure != null) {
                _cts         = new CancellationTokenSource();
                _measureTask = Task.Run(() => MeasureGap(_cts.Token), _cts.Token);
            }

            var checkpoint = await _checkpointStore.GetLastCheckpoint(SubscriptionId, cancellationToken).Ignore();

            _lastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

            Subscription = await Subscribe(checkpoint, cancellationToken).Ignore();

            IsRunning = true;

            Log?.LogInformation("Started subscription {Subscription}", SubscriptionId);
        }

        protected async Task Handler(ReceivedEvent re, CancellationToken cancellationToken) {
            DebugLog?.Invoke(
                "Subscription {Subscription} got an event {EventType}",
                SubscriptionId,
                re.EventType
            );

            _lastProcessed = GetPosition(re);

            if (re.EventType.StartsWith("$") || re.Data.IsEmpty) {
                await Store().Ignore();
                return;
            }

            object? evt;

            var contentType = string.IsNullOrWhiteSpace(re.ContentType) ? "application/json" : re.ContentType;

            if (contentType != _eventSerializer.ContentType) {
                Log?.LogError(
                    "Unknown content type {ContentType} for event {Strean} {Position} {Type}",
                    contentType,
                    re.Stream,
                    re.StreamPosition,
                    re.EventType
                );

                if (_throwOnError)
                    throw new InvalidOperationException($"Unknown content type {contentType}");
            }

            try {
                evt = _eventSerializer.Deserialize(re.Data.Span, re.EventType);
            }
            catch (Exception e) {
                Log?.LogError(
                    e,
                    "Error deserializing event {Strean} {Position} {Type}",
                    re.Stream,
                    re.StreamPosition,
                    re.EventType
                );

                if (_throwOnError)
                    throw new DeserializationException(re, e);

                return;
            }

            try {
                if (evt != null) {
                    await Task.WhenAll(
                        _eventHandlers.Select(x => x.HandleEvent(evt, (long?) re.StreamPosition, cancellationToken))
                    ).Ignore();
                }
            }
            catch (Exception e) {
                Log?.LogWarning(
                    e,
                    "Error when handling the event {Stream} {Position} {Type}",
                    re.Stream,
                    re.StreamPosition,
                    re.EventType
                );

                if (_throwOnError)
                    throw new SubscriptionException(re, evt, e);
            }

            await Store().Ignore();

            Task Store() => StoreCheckpoint(GetPosition(re), cancellationToken);

            static EventPosition GetPosition(ReceivedEvent receivedEvent)
                => new(receivedEvent.StreamPosition, receivedEvent.Created);
        }

        protected async Task StoreCheckpoint(EventPosition position, CancellationToken cancellationToken) {
            _lastProcessed = position;
            var checkpoint = new Checkpoint(SubscriptionId, position.Position);

            await _checkpointStore.StoreCheckpoint(checkpoint, cancellationToken).Ignore();
        }

        protected abstract Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        );

        public async Task StopAsync(CancellationToken cancellationToken) {
            IsRunning = false;

            if (_measureTask != null) {
                _cts?.Cancel();

                try {
                    await _measureTask.Ignore();
                }
                catch (OperationCanceledException) {
                    // Expected
                }
            }

            await Subscription.Stop(cancellationToken).Ignore();

            Log?.LogInformation("Stopped subscription {Subscription}", SubscriptionId);
        }

        readonly InterlockedSemaphore _resubscribing = new();

        protected async Task Resubscribe(TimeSpan delay) {
            Log?.LogWarning("Resubscribing {Subscription}", SubscriptionId);

            await Task.Delay(delay).Ignore();

            while (IsRunning && IsDropped && _resubscribing.CanMove()) {
                try {
                    var checkpoint = new Checkpoint(SubscriptionId, _lastProcessed?.Position);

                    Subscription = await Subscribe(checkpoint, CancellationToken.None).Ignore();

                    IsDropped = false;
                    _resubscribing.Open();

                    Log?.LogInformation("Subscription {Subscription} restored", SubscriptionId);
                }
                catch (Exception e) {
                    Log?.LogError(e, "Unable to restart the subscription {Subscription}", SubscriptionId);

                    await Task.Delay(1000).Ignore();
                }
            }
        }

        protected void Dropped(
            DropReason reason,
            Exception? exception
        ) {
            if (!IsRunning || _resubscribing.IsClosed()) return;

            Log?.LogWarning(
                exception,
                "Subscription {Subscription} dropped {Reason}",
                SubscriptionId,
                reason
            );

            IsDropped      = true;
            _lastException = exception;

            Task.Run(
                () => Resubscribe(
                    reason == DropReason.Stopped ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2)
                )
            );
        }

        async Task MeasureGap(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                var (position, created) = await GetLastEventPosition(cancellationToken).Ignore();

                if (_lastProcessed?.Position != null && position != null) {
                    _gap = (ulong) position - _lastProcessed.Position.Value;

                    _measure!.PutGap(SubscriptionId, _gap, created);
                }

                await Task.Delay(1000, cancellationToken).Ignore();
            }
        }

        protected abstract Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken);

        Exception? _lastException;

        public SubscriptionHealth Health => new(!(IsRunning && IsDropped), _lastException);
    }

    public record EventPosition(ulong? Position, DateTime Created);
}