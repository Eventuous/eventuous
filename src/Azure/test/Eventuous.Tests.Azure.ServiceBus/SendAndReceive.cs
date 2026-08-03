using Eventuous.Azure.ServiceBus.Producers;
using Eventuous.Azure.ServiceBus.Subscriptions;
using Eventuous.Producers;

namespace Eventuous.Tests.Azure.ServiceBus;

[NotInParallel]
[TopicAndQueueSource]
public class SendAndReceive {
    ServiceBusProducer     _producer     = null!;
    ServiceBusSubscription _subscription = null!;

    readonly string                        _correlationId;
    readonly Metadata                      _metadata;
    readonly TestEventHandler              _handler = new();
    readonly AzureServiceBusFixture        _fixture;
    readonly StreamName                    _streamName;
    readonly ServiceBusProducerOptions     _serviceBusProducerOptions;
    readonly ServiceBusSubscriptionOptions _serviceBusSubscriptionOptions;

    public SendAndReceive(AzureServiceBusFixture fixture, ServiceBusProducerOptions producerOptions, ServiceBusSubscriptionOptions subscriptionOptions) {
        _streamName                    = new(producerOptions.QueueOrTopicName);
        _correlationId                 = Guid.NewGuid().ToString();
        _metadata                      = new Metadata().With(MetaTags.CorrelationId, _correlationId);
        _serviceBusProducerOptions     = producerOptions;
        _serviceBusSubscriptionOptions = subscriptionOptions;
        _fixture                       = fixture;
    }

    [Test]
    [Retry(3)]
    public async Task SingleMessage(CancellationToken cancellationToken) {
        await _producer.Produce(_streamName, SomeEvent.Create(), _metadata, cancellationToken: cancellationToken);

        // Assert
        await _handler.AssertThat()
            .Timebox(TimeSpan.FromSeconds(5))
            .Single()
            .Match(evt => evt is SomeEvent)
            .Validate(cancellationToken);
    }

    [Test]
    [Retry(3)]
    public async Task LoadsOfMessages(CancellationToken cancellationToken) {
        const int count = 200;

        var events = Enumerable.Range(0, count).Select(SomeEvent.Create).ToList();
        await _producer.Produce(_streamName, events, _metadata, cancellationToken: cancellationToken);

        // Assert
        await _handler.AssertThat()
            .Timebox(TimeSpan.FromSeconds(20))
            .Exactly(count)
            .Match(evt => evt is SomeEvent)
            .Validate(cancellationToken);

        var handledMessageIds = _handler.Messages
            .OfType<SomeEvent>()
            .Select(m => m.Id)
            .Order()
            .ToList();
        await Assert.That(handledMessageIds).IsEquivalentTo(events.Select(e => e.Id));
    }

    [After(Test)]
    public async ValueTask CleanUpProducerAndSubscription(CancellationToken cancellationToken) {
        await _producer.StopAsync(cancellationToken);
        await _subscription.Unsubscribe(_ => { }, cancellationToken);
        await _subscription.DisposeAsync();
        await _producer.DisposeAsync();
    }

    [Before(Test)]
    public async Task StartProducerAndSubscription(CancellationToken cancellationToken) {
        _producer     = _fixture.CreateProducer(_serviceBusProducerOptions);
        _subscription = _fixture.CreateSubscription(_serviceBusSubscriptionOptions, _handler, _correlationId);

        await _producer.StartAsync(cancellationToken);
        await _subscription.Subscribe(_ => { }, (_, _, _) => { }, cancellationToken);
    }
}
