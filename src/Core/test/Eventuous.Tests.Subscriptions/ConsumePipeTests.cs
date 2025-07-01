using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.Tests.Subscriptions;

public class ConsumePipeTests() {
    [Test]
    public async Task ShouldCallHandlers() {
        var handler = new TestHandler();
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);
        var ctx     = TestContext.CreateContext();

        await pipe.Send(ctx);

        await Assert.That(handler.Called).IsEqualTo(1);
    }

    const string Key = "test-baggage";

    [Test]
    public async Task ShouldAddContextBaggage() {
        var handler = new TestHandler();
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);
        var baggage = Guid.NewGuid().ToString();

        pipe.AddFilterFirst(new TestFilter(Key, baggage));

        var ctx = TestContext.CreateContext();

        await pipe.Send(ctx);

        await Assert.That(handler.Called).IsEqualTo(1);
        await Assert.That(handler.Received).IsNotNull();
        await Assert.That(handler.Received!.Items.GetItem<string>(Key)).IsEqualTo(baggage);
    }

    class TestFilter(string key, string payload) : ConsumeFilter<IMessageConsumeContext> {
        protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
            context.Items.AddItem(key, payload);

            return next?.Value.Send(context, next.Next) ?? default;
        }
    }

    class TestHandler : BaseEventHandler {
        public int                     Called   { get; private set; }
        public IMessageConsumeContext? Received { get; private set; }

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            Called++;
            Received = context;

            return default;
        }
    }
}
