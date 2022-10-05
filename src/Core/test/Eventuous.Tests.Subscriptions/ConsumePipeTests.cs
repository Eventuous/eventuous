using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.Tests.Subscriptions;

public class ConsumePipeTests {
    readonly ITestOutputHelper _outputHelper;

    static readonly Fixture Auto = new();

    public ConsumePipeTests(ITestOutputHelper outputHelper) => _outputHelper = outputHelper;

    [Fact]
    public async Task ShouldCallHandlers() {
        var handler = new TestHandler();
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);

        var ctx = Auto.CreateContext(_outputHelper);

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

        var ctx = Auto.CreateContext(_outputHelper);

        await pipe.Send(ctx);

        handler.Called.Should().Be(1);
        handler.Received.Should().NotBeNull();
        handler.Received!.Items.GetItem<string>(Key).Should().Be(baggage);
    }

    class TestFilter : ConsumeFilter<IMessageConsumeContext> {
        readonly string _key;
        readonly string _payload;

        public TestFilter(string key, string payload) {
            _key     = key;
            _payload = payload;
        }

        protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
            context.Items.AddItem(_key, _payload);
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
