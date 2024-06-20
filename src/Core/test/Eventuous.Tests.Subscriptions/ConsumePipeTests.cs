using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.Tests.Subscriptions;

public class ConsumePipeTests(ITestOutputHelper outputHelper) {
    static readonly Fixture Auto = new();

    [Fact]
    public async Task ShouldCallHandlers() {
        var handler = new TestHandler();
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);
        var ctx     = Auto.CreateContext(outputHelper);

        await pipe.Send(ctx);

        handler.Called.Should().Be(1);
    }

    const string Key = "test-baggage";

    [Fact]
    public async Task ShouldAddContextBaggage() {
        var handler = new TestHandler();
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);
        var baggage = Auto.Create<string>();

        pipe.AddFilterFirst(new TestFilter(Key, baggage));

        var ctx = Auto.CreateContext(outputHelper);

        await pipe.Send(ctx);

        handler.Called.Should().Be(1);
        handler.Received.Should().NotBeNull();
        handler.Received!.Items.GetItem<string>(Key).Should().Be(baggage);
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
