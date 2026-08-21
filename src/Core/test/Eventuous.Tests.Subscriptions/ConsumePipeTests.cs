using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.Tests.Subscriptions;

public class ConsumePipeTests {
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

    [Test]
    public async Task ShouldMakeSecondDisposalWaitForTheFirst() {
        var filter = new BlockingFilter();
        var pipe   = new ConsumePipe().AddFilterFirst(filter);

        var first  = pipe.DisposeAsync();
        var second = pipe.DisposeAsync();

        // The pipe is still disposing, so a second caller must not be told the teardown is done
        await Assert.That(second.IsCompleted).IsFalse();

        filter.Release();
        await first;
        await second;

        await Assert.That(filter.Disposals).IsEqualTo(1);
    }

    /// <summary>
    /// Blocks inside <see cref="DisposeAsync"/> until released, so a second disposal can be observed while the
    /// first one is still running.
    /// </summary>
    class BlockingFilter : ConsumeFilter<IMessageConsumeContext>, IAsyncDisposable {
        readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Disposals { get; private set; }

        public void Release() => _release.TrySetResult();

        protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next)
            => next == null ? default : next.Value.Send(context, next.Next);

        public async ValueTask DisposeAsync() {
            Disposals++;

            await _release.Task;
        }
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
