// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class SubscriptionIgnoredMessagesTests(StoreFixture fixture, ITestOutputHelper outputHelper) : IClassFixture<StoreFixture> {
    readonly ILoggerFactory      _loggerFactory   = TestHelpers.Logging.GetLoggerFactory(outputHelper);
    readonly TestCheckpointStore _checkpointStore = new();

    [Fact]
    public async Task SubscribeAndProduceManyWithIgnored() {
        StreamName stream = new($"test-{Guid.NewGuid():N}");
        const int  count  = 10;

        var testEvents = Generate().ToList();
        var handler    = new TestEventHandler(TimeSpan.FromMilliseconds(5));
        var producer   = new EventStoreProducer(fixture.Client);

        TypeMap.Instance.AddType<TestEvent>(TestEvent.TypeName);
        TypeMap.Instance.AddType<UnknownEvent>("ignored");
        await producer.Produce(stream, testEvents, new Metadata());

        var subscriptionId = $"test-{Guid.NewGuid():N}";
        var pipe           = new ConsumePipe();
        pipe.AddDefaultConsumer(handler);

        var subscription = new StreamSubscription(
            fixture.Client,
            new StreamSubscriptionOptions {
                StreamName     = stream,
                SubscriptionId = subscriptionId
            },
            _checkpointStore,
            pipe,
            _loggerFactory
        );

        var log = _loggerFactory.CreateLogger("Subscription");
        TypeMap.Instance.RemoveType<UnknownEvent>();

        var expected = testEvents.Where(x => x.GetType() == typeof(TestEvent)).ToList();
        await subscription.SubscribeWithLog(log);
        await handler.AssertCollection(5.Seconds(), expected).Validate();
        await subscription.UnsubscribeWithLog(log);

        var last = await _checkpointStore.GetLastCheckpoint(subscriptionId, default);
        last.Position.Should().Be((ulong)(testEvents.Count - 1));

        return;

        IEnumerable<object> Generate() {
            for (var i = 0; i < count; i++) {
                yield return new TestEvent(fixture.Auto.Create<string>(), i);
                yield return new UnknownEvent(fixture.Auto.Create<string>(), i);
            }
        }
    }

    record UnknownEvent(string Data, int Number);
}
