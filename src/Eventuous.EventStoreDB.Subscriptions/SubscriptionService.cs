using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.EventStoreDB.Subscriptions {
    [PublicAPI]
    public abstract class SubscriptionService : IHostedService, IHealthCheck {
        readonly ICheckpointStore        _checkpointStore;
        readonly IEventSerializer        _eventSerializer;
        readonly IEventHandler[]         _projections;
        readonly string                  _subscriptionId;
        readonly SubscriptionGapMeasure? _measure;
        readonly ILogger?                _log;
        readonly Log?                    _debugLog;

        StreamSubscription      _subscription;
        CancellationTokenSource _cts;
        Task?                   _measureTask;
        ulong?                  _lastProcessedPosition;
        ulong                   _gap;
        bool                    _running;
        bool                    _dropped;

        protected EventStoreClient EventStoreClient { get; }

        protected SubscriptionService(
            EventStoreClient           eventStoreClient,
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) {
            EventStoreClient = eventStoreClient;
            _checkpointStore = checkpointStore;
            _eventSerializer = eventSerializer;
            _subscriptionId  = subscriptionId;
            _measure         = measure;

            _projections = eventHandlers.Where(x => x.SubscriptionId == subscriptionId).ToArray();

            _log = loggerFactory?.CreateLogger($"StreamSubscription-{subscriptionId}");

            _debugLog = _log?.IsEnabled(LogLevel.Debug) == true ? _log.LogDebug : null;
        }

        public async Task StartAsync(
            CancellationToken cancellationToken
        ) {
            var checkpoint = await _checkpointStore.GetLastCheckpoint(_subscriptionId, cancellationToken);

            _lastProcessedPosition = checkpoint.Position;

            _subscription = await Subscribe(checkpoint, cancellationToken);

            if (_measure != null) {
                _cts         = new CancellationTokenSource();
                _measureTask = Task.Run(() => MeasureGap(_cts.Token), _cts.Token);
            }

            _running = true;

            _log.LogInformation("Started subscription {Subscription}", _subscriptionId);
        }

        protected async Task Handler(StreamSubscription sub, ResolvedEvent re, CancellationToken cancellationToken) {
            _debugLog?.Invoke(
                "Subscription {Subscription} got an event {@Event}",
                sub.SubscriptionId,
                re
            );

            _lastProcessedPosition = GetPosition(re);

            if (re.Event.EventType.StartsWith("$")) {
                await Store();
            }

            try {
                var evt = _eventSerializer.Deserialize(re.Event.Data.Span, re.Event.EventType);

                if (evt != null) {
                    _debugLog?.Invoke("Handling event {Event}", evt);

                    await Task.WhenAll(
                        _projections.Select(
                            x => x.HandleEvent(evt, (long?) re.OriginalPosition?.CommitPosition)
                        )
                    );
                }
            }
            catch (Exception e) {
                _log.LogWarning(e, "Error when handling the event {Event}", re.Event.EventType);
            }

            await Store();

            Task Store() => StoreCheckpoint(GetPosition(re), cancellationToken);
        }

        protected async Task StoreCheckpoint(ulong? position, CancellationToken cancellationToken) {
            _lastProcessedPosition = position;

            var checkpoint = new Checkpoint(_subscriptionId, position);

            await _checkpointStore.StoreCheckpoint(checkpoint, cancellationToken);
        }

        protected abstract ulong? GetPosition(ResolvedEvent resolvedEvent);

        protected abstract Task<StreamSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        );

        public async Task StopAsync(CancellationToken cancellationToken) {
            _running = false;

            if (_measureTask != null) {
                _cts.Cancel();

                try {
                    await _measureTask;
                }
                catch (OperationCanceledException) {
                    // Expected
                }
            }

            _subscription.Dispose();

            _log.LogInformation("Stopped subscription {Subscription}", _subscriptionId);
        }

        async Task Resubscribe(TimeSpan delay) {
            _log.LogWarning("Resubscribing {Subscription}", _subscriptionId);

            await Task.Delay(delay);

            while (_running && _dropped) {
                try {
                    var checkpoint = new Checkpoint(_subscriptionId, _lastProcessedPosition);

                    _subscription = await Subscribe(checkpoint, CancellationToken.None);

                    _dropped = false;

                    _log.LogInformation("Subscription {Subscription} restored", _subscriptionId);
                }
                catch (Exception e) {
                    _log.LogError(e, "Unable to restart the subscription {Subscription}", _subscriptionId);

                    await Task.Delay(1000);
                }
            }
        }

        protected void Dropped(
            StreamSubscription        _,
            SubscriptionDroppedReason reason,
            Exception?                exception
        ) {
            if (!_running) return;

            _log.LogWarning(
                exception,
                "Subscription {Subscription} dropped {Reason}",
                _subscriptionId,
                reason
            );

            _dropped = true;

            Task.Run(
                () => Resubscribe(
                    reason == SubscriptionDroppedReason.Disposed ? TimeSpan.FromSeconds(10) : TimeSpan.Zero
                )
            );
        }

        async Task MeasureGap(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                var lastEventRead = EventStoreClient.ReadAllAsync(
                    Direction.Backwards,
                    Position.End,
                    1,
                    cancellationToken: cancellationToken
                );

                var events = await lastEventRead.ToArrayAsync(cancellationToken);

                var lastPosition = events[0].OriginalPosition?.CommitPosition;

                if (_lastProcessedPosition != null && lastPosition != null) {
                    _gap = (ulong) lastPosition - _lastProcessedPosition.Value;

                    _measure!.PutGap(_subscriptionId, _gap);
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken  cancellationToken = default
        ) {
            var result = _running && _dropped
                ? HealthCheckResult.Unhealthy("Subscription dropped")
                : HealthCheckResult.Healthy();

            return Task.FromResult(result);
        }
    }
}