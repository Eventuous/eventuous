using Eventuous.Producers;
using JetBrains.Annotations;

namespace Eventuous.Tests.Gateway;

public class MultiStreamProduceTests {
    [Test]
    public async Task ShouldProduceToMultipleStreams() {
        var producer = new TestProducer();
        var stream1 = new StreamName("stream-1");
        var stream2 = new StreamName("stream-2");
        var msg1 = new ProducedMessage("event-1", null);
        var msg2 = new ProducedMessage("event-2", null);

        var requests = new ProduceRequest<TestProduceOptions>[] {
            new(stream1, [msg1], null),
            new(stream2, [msg2], null)
        };

        await producer.Produce(requests);

        await Assert.That(producer.ProducedMessages).HasCount().EqualTo(2);
        await Assert.That(producer.Streams).Contains(stream1);
        await Assert.That(producer.Streams).Contains(stream2);
    }

    [Test]
    public async Task ShouldHandleEmptyRequests() {
        var producer = new TestProducer();

        await producer.Produce(Array.Empty<ProduceRequest<TestProduceOptions>>());

        await Assert.That(producer.ProducedMessages).HasCount().EqualTo(0);
    }

    [Test]
    public async Task ShouldProduceMultipleMessagesToSameStream() {
        var producer = new TestProducer();
        var stream = new StreamName("stream-1");
        var msg1 = new ProducedMessage("event-1", null);
        var msg2 = new ProducedMessage("event-2", null);

        var requests = new ProduceRequest<TestProduceOptions>[] {
            new(stream, [msg1, msg2], null)
        };

        await producer.Produce(requests);

        await Assert.That(producer.ProducedMessages).HasCount().EqualTo(2);
        await Assert.That(producer.Streams.Distinct().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task ShouldProduceUntypedRequests() {
        var producer = new TestProducer();
        var stream1 = new StreamName("stream-1");
        var stream2 = new StreamName("stream-2");
        var msg1 = new ProducedMessage("event-1", null);
        var msg2 = new ProducedMessage("event-2", null);

        var requests = new ProduceRequest[] {
            new(stream1, [msg1]),
            new(stream2, [msg2])
        };

        await producer.Produce(requests);

        await Assert.That(producer.ProducedMessages).HasCount().EqualTo(2);
        await Assert.That(producer.Streams).Contains(stream1);
        await Assert.That(producer.Streams).Contains(stream2);
    }

    class TestProducer : BaseProducer<TestProduceOptions> {
        public List<ProducedMessage> ProducedMessages { get; } = [];
        public List<StreamName> Streams { get; } = [];

        protected override Task ProduceMessages(
                StreamName                   stream,
                IEnumerable<ProducedMessage> messages,
                TestProduceOptions?          options,
                CancellationToken            cancellationToken = default
            ) {
            Streams.Add(stream);
            ProducedMessages.AddRange(messages);

            return Task.CompletedTask;
        }
    }

    [UsedImplicitly]
    record TestProduceOptions;
}
