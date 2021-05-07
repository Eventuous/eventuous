using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions {
    [PublicAPI]
    public abstract class SubscriptionService : IHostedService, IHealthCheck {
        protected bool              IsRunning      { get; set; }
        protected bool              IsDropped      { get; set; }
        protected EventSubscription Subscription   { get; set; } = null!;
        protected string            SubscriptionId { get; }
        protected Logging?          DebugLog       { get; }
        protected ILogger?          Log            { get; }

        readonly ICheckpointStore         _checkpointStore;
        readonly IEventSerializer         _eventSerializer;
        readonly IEventHandler[]          _eventHandlers;
        readonly ISubscriptionGapMeasure? _measure;

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
        ) : this(
            Ensure.NotEmptyString(options.SubscriptionId, nameof(options.SubscriptionId)),
            checkpointStore,
            eventHandlers,
            eventSerializer,
            loggerFactory,
            measure
        ) { }

        protected SubscriptionService(
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer?          eventSerializer = null,
            ILoggerFactory?            loggerFactory   = null,
            ISubscriptionGapMeasure?   measure         = null
        ) {
            _checkpointStore = Ensure.NotNull(checkpointStore, nameof(checkpointStore));
            _eventSerializer = eventSerializer ?? DefaultEventSerializer.Instance;
            SubscriptionId   = Ensure.NotEmptyString(subscriptionId, subscriptionId);
            _measure         = measure;

            _eventHandlers = Ensure.NotNull(eventHandlers, nameof(eventHandlers))
                .Where(x => x.SubscriptionId == subscriptionId)
                .ToArray();

            Log = loggerFactory?.CreateLogger($"StreamSubscription-{subscriptionId}");

            DebugLog = Log?.IsEnabled(LogLevel.Debug) == true ? Log.LogDebug : null;
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            if (_measure != null) {
                _cts         = new CancellationTokenSource();
                _measureTask = Task.Run(() => MeasureGap(_cts.Token), _cts.Token);
            }

            var checkpoint = await _checkpointStore.GetLastCheckpoint(SubscriptionId, cancellationToken);

            _lastProcessed = new EventPosition(checkpoint.Position, DateTime.Now);

            Subscription = await Subscribe(checkpoint, cancellationToken);

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
                await Store();
                return;
            }

            try {
                var contentType = string.IsNullOrWhiteSpace(re.ContentType) ? "application/json" : re.ContentType;

                if (contentType != _eventSerializer.ContentType)
                    throw new InvalidOperationException($"Unknown content type {contentType}");

                object? evt;

                try {
                    evt = _eventSerializer.Deserialize(re.Data.Span, re.EventType);
                }
                catch (Exception e) {
                    Log?.LogError(
                        e,
                        "Error deserializing event {Strean} {Position} {Type}",
                        re.OriginalStream,
                        re.StreamPosition,
                        re.EventType
                    );

                    throw;
                }

                if (evt != null) {
                    await Task.WhenAll(
                        _eventHandlers.Select(x => x.HandleEvent(evt, (long?) re.GlobalPosition, cancellationToken))
                    );
                }
            }
            catch (Exception e) {
                Log?.LogWarning(
                    e,
                    "Error when handling the event {Strean} {Position} {Type}",
                    re.OriginalStream,
                    re.StreamPosition,
                    re.EventType
                );
            }

            await Store();

            Task Store() => StoreCheckpoint(GetPosition(re), cancellationToken);

            static EventPosition GetPosition(ReceivedEvent receivedEvent)
                => new(receivedEvent.StreamPosition, receivedEvent.Created);
        }

        protected async Task StoreCheckpoint(EventPosition position, CancellationToken cancellationToken) {
            _lastProcessed = position;
            var checkpoint = new Checkpoint(SubscriptionId, position.Position);

            await _checkpointStore.StoreCheckpoint(checkpoint, cancellationToken);
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
                    await _measureTask;
                }
                catch (OperationCanceledException) {
                    // Expected
                }
            }

            await Subscription.Stop(cancellationToken);

            Log?.LogInformation("Stopped subscription {Subscription}", SubscriptionId);
        }

        protected async Task Resubscribe(TimeSpan delay) {
            Log?.LogWarning("Resubscribing {Subscription}", SubscriptionId);

            await Task.Delay(delay);

            while (IsRunning && IsDropped) {
                try {
                    var checkpoint = new Checkpoint(SubscriptionId, _lastProcessed?.Position);

                    Subscription = await Subscribe(checkpoint, CancellationToken.None);

                    IsDropped = false;

                    Log?.LogInformation("Subscription {Subscription} restored", SubscriptionId);
                }
                catch (Exception e) {
                    Log?.LogError(e, "Unable to restart the subscription {Subscription}", SubscriptionId);

                    await Task.Delay(1000);
                }
            }
        }

        protected void Dropped(
            DropReason reason,
            Exception? exception
        ) {
            if (!IsRunning) return;

            Log?.LogWarning(
                exception,
                "Subscription {Subscription} dropped {Reason}",
                SubscriptionId,
                reason
            );

            IsDropped = true;

            Task.Run(
                () => Resubscribe(
                    reason == DropReason.Stopped ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(2)
                )
            );
        }

        async Task MeasureGap(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                var (position, created) = await GetLastEventPosition(cancellationToken);

                if (_lastProcessed?.Position != null && position != null) {
                    _gap = (ulong) position - _lastProcessed.Position.Value;

                    _measure!.PutGap(SubscriptionId, _gap, created);
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        protected abstract Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken);

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken  cancellationToken = default
        ) {
            var result = IsRunning && IsDropped
                ? HealthCheckResult.Unhealthy("Subscription dropped")
                : HealthCheckResult.Healthy();

            return Task.FromResult(result);
        }
    }

    public record EventPosition(ulong? Position, DateTime Created);
}