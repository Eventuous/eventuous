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
        protected bool                IsRunning      { get; set; }
        protected bool                IsDropped      { get; set; }
        protected MessageSubscription Subscription   { get; set; } = null!;
        protected string              SubscriptionId { get; }

        readonly ICheckpointStore        _checkpointStore;
        readonly IEventSerializer        _eventSerializer;
        readonly IEventHandler[]         _projections;
        readonly SubscriptionGapMeasure? _measure;
        readonly ILogger?                _log;
        readonly Log?                    _debugLog;

        CancellationTokenSource? _cts;
        Task?                    _measureTask;
        MessagePosition?         _lastProcessed;
        ulong                    _gap;

        protected SubscriptionService(
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) {
            _checkpointStore = checkpointStore;
            _eventSerializer = eventSerializer;
            SubscriptionId   = subscriptionId;
            _measure         = measure;

            _projections = eventHandlers.Where(x => x.SubscriptionId == subscriptionId).ToArray();

            _log = loggerFactory?.CreateLogger($"StreamSubscription-{subscriptionId}");

            _debugLog = _log?.IsEnabled(LogLevel.Debug) == true ? _log.LogDebug : null;
        }

        public async Task StartAsync(
            CancellationToken cancellationToken
        ) {
            var checkpoint = await _checkpointStore.GetLastCheckpoint(SubscriptionId, cancellationToken);

            _lastProcessed = new MessagePosition(checkpoint.Position, DateTime.Now);

            Subscription = await Subscribe(checkpoint, cancellationToken);

            if (_measure != null) {
                _cts         = new CancellationTokenSource();
                _measureTask = Task.Run(() => MeasureGap(_cts.Token), _cts.Token);
            }

            IsRunning = true;

            _log.LogInformation("Started subscription {Subscription}", SubscriptionId);
        }

        protected async Task Handler(ReceivedMessage re, CancellationToken cancellationToken) {
            _debugLog?.Invoke(
                "Subscription {Subscription} got an event {@Event}",
                SubscriptionId,
                re
            );

            _lastProcessed = GetPosition(re);

            if (re.MessageType.StartsWith("$")) {
                await Store();
            }

            try {
                var evt = _eventSerializer.Deserialize(re.Data.Span, re.MessageType);

                if (evt != null) {
                    _debugLog?.Invoke("Handling event {Event}", evt);

                    await Task.WhenAll(
                        _projections.Select(x => x.HandleEvent(evt, (long?) re.Position))
                    );
                }
            }
            catch (Exception e) {
                _log.LogWarning(e, "Error when handling the event {Event}", re.MessageType);
            }

            await Store();

            Task Store() => StoreCheckpoint(GetPosition(re), cancellationToken);
        }

        protected async Task StoreCheckpoint(MessagePosition position, CancellationToken cancellationToken) {
            _lastProcessed = position;
            var checkpoint = new Checkpoint(SubscriptionId, position.Position);

            await _checkpointStore.StoreCheckpoint(checkpoint, cancellationToken);
        }

        static MessagePosition GetPosition(ReceivedMessage receivedMessage)
            => new(receivedMessage.Sequence, receivedMessage.Created);

        protected abstract Task<MessageSubscription> Subscribe(
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

            Subscription.Dispose();

            _log.LogInformation("Stopped subscription {Subscription}", SubscriptionId);
        }

        protected async Task Resubscribe(TimeSpan delay) {
            _log.LogWarning("Resubscribing {Subscription}", SubscriptionId);

            await Task.Delay(delay);

            while (IsRunning && IsDropped) {
                try {
                    var checkpoint = new Checkpoint(SubscriptionId, _lastProcessed?.Position);

                    Subscription = await Subscribe(checkpoint, CancellationToken.None);

                    IsDropped = false;

                    _log.LogInformation("Subscription {Subscription} restored", SubscriptionId);
                }
                catch (Exception e) {
                    _log.LogError(e, "Unable to restart the subscription {Subscription}", SubscriptionId);

                    await Task.Delay(1000);
                }
            }
        }

        protected void Dropped(
            DropReason reason,
            Exception? exception
        ) {
            if (!IsRunning) return;

            _log.LogWarning(
                exception,
                "Subscription {Subscription} dropped {Reason}",
                SubscriptionId,
                reason
            );

            IsDropped = true;

            Task.Run(
                () => Resubscribe(
                    reason == DropReason.Stopped ? TimeSpan.FromSeconds(10) : TimeSpan.Zero
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

        protected abstract Task<MessagePosition> GetLastEventPosition(CancellationToken cancellationToken);

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

    public class MessageSubscription : IDisposable {
        readonly IDisposable _inner;

        public MessageSubscription(string subscriptionId, IDisposable inner) {
            _inner         = inner;
            SubscriptionId = subscriptionId;
        }

        public string SubscriptionId { get; }

        public void Dispose() => _inner.Dispose();
    }

    public record MessagePosition(ulong? Position, DateTime Created);
}