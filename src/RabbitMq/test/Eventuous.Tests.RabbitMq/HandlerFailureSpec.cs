using System.Collections.Concurrent;
using Eventuous.Producers;
using Eventuous.RabbitMq.Producers;
using Eventuous.RabbitMq.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.TestHelpers.TUnit;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.RabbitMq;

/// <summary>
/// With <c>ThrowOnError</c>, a failing handler must end the run so the supervisor replaces it, and the
/// delivery it never acknowledged must come back. Before the run was told about the failure, the throw only
/// reached the client's <c>CallbackException</c> event on one path and the handling filter's channel worker
/// on the other: no drop was reported, no resubscribe happened, and the delivery stayed undecided.
/// </summary>
[ClassDataSource<RabbitMqFixture>]
public class HandlerFailureSpec {
    static HandlerFailureSpec() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    const int EventCount = 3;

    RabbitMqProducer                     _producer     = null!;
    FailFirstHandler                     _handler      = null!;
#pragma warning disable TUnit0023
    RabbitMqSubscription                 _subscription = null!;
    TestEventListener                    _es           = null!;
#pragma warning restore TUnit0023
    readonly StreamName                  _exchange;
    readonly ILogger<HandlerFailureSpec> _log;
    readonly ILoggerFactory              _loggerFactory;
    readonly RabbitMqFixture             _fixture;

    int _subscribed;
    int _dropped;

    public HandlerFailureSpec(RabbitMqFixture fixture) {
        _fixture       = fixture;
        _exchange      = new(Guid.NewGuid().ToString());
        _loggerFactory = LoggingExtensions.GetLoggerFactory();
        _log           = _loggerFactory.CreateLogger<HandlerFailureSpec>();
    }

    [Test]
    public async Task Resubscribes_and_redelivers_the_event_the_handler_failed_on(CancellationToken cancellationToken) {
        await StartSubscription();

        await AssertRecovery(cancellationToken);
    }

    /// <summary>
    /// The failure handler is user code, so it can throw too. That throw must not cost the run its failure —
    /// the filter would log it and leave a subscription that reports healthy and consumes nothing.
    /// </summary>
    [Test]
    public async Task Resubscribes_when_the_failure_handler_itself_throws(CancellationToken cancellationToken) {
        await StartSubscription((_, _, _) => throw new InvalidOperationException("Simulated failure handler failure"));

        await AssertRecovery(cancellationToken);
    }

    async Task AssertRecovery(CancellationToken cancellationToken) {
        var testEvents = TestEvent.CreateMany(EventCount).ToArray();
        await _producer.Produce(_exchange, testEvents, new(), cancellationToken: cancellationToken);

        // Room for the failure, the one-second retry delay, the reconnect and the redelivery.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try {
            while (_handler.Handled.Count < EventCount) await Task.Delay(100, cts.Token);
        } catch (OperationCanceledException) when (cts.Token.IsCancellationRequested) {
            // Fall through to the assertions, which say more about what went wrong than a cancellation would.
        }

        await Assert.That(_handler.HasFailed).IsTrue();

        // The event the handler threw on was never acknowledged, so only a resubscribe can bring it back.
        // Its absence is the regression: the subscription keeps its connection and quietly loses the message.
        await Assert.That(_handler.Handled.Order()).IsEquivalentTo(testEvents.Select(x => x.Number).Order());

        await Assert.That(Volatile.Read(ref _dropped)).IsGreaterThanOrEqualTo(1);
        await Assert.That(Volatile.Read(ref _subscribed)).IsGreaterThanOrEqualTo(2);
    }

    [Before(Test)]
    public async ValueTask InitializeAsync() {
        _es       = new();
        _handler  = new();
        _producer = new(_fixture.ConnectionFactory);

        await _producer.StartAsync();
    }

    /// <summary>
    /// Subscribed before anything is produced: the subscription is what declares the queue and binds it, so a
    /// publish that beats it goes nowhere.
    /// </summary>
    async Task StartSubscription(RabbitMqSubscription.HandleEventProcessingFailure? failureHandler = null) {
        var queue = Guid.NewGuid().ToString();

        _subscription = new(
            _fixture.ConnectionFactory,
            new RabbitMqSubscriptionOptions {
                ConcurrencyLimit = 1,
                SubscriptionId   = queue,
                Exchange         = _exchange,
                ThrowOnError     = true,
                RetryDelay       = TimeSpan.FromSeconds(1),
                FailureHandler   = failureHandler
            },
            new ConsumePipe().AddDefaultConsumer(_handler),
            _loggerFactory
        );

        await _subscription.Subscribe(
            id => {
                Interlocked.Increment(ref _subscribed);
                _log.LogInformation("{Subscription} subscribed", id);
            },
            (id, reason, ex) => {
                Interlocked.Increment(ref _dropped);
                _log.LogWarning(ex, "{Subscription} dropped {Reason}", id, reason);
            },
            CancellationToken.None
        );
    }

    [After(Test)]
    public async ValueTask DisposeAsync() {
        await _producer.StopAsync();
        await _subscription.UnsubscribeWithLog(_log);
        _es.Dispose();
        await _subscription.DisposeAsync();
    }

    /// <summary>
    /// Throws on the first event it sees and records every event it handles, so a test can tell a redelivery
    /// of the failed event from the events that merely followed it.
    /// </summary>
    sealed class FailFirstHandler : BaseEventHandler {
        readonly ConcurrentDictionary<int, byte> _handled = [];

        int _failed;

        public bool HasFailed => Volatile.Read(ref _failed) > 0;

        public IReadOnlyCollection<int> Handled => _handled.Keys.ToArray();

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            if (Interlocked.CompareExchange(ref _failed, 1, 0) == 0) {
                throw new InvalidOperationException("Simulated handler failure on the first event");
            }

            if (context.Message is TestEvent evt) _handled.TryAdd(evt.Number, 0);

            return new(EventHandlingStatus.Success);
        }
    }
}
