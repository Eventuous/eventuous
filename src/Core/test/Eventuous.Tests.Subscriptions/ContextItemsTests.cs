using Eventuous.Subscriptions.Context;

namespace Eventuous.Tests.Subscriptions;

public class ContextItemsTests {
    [Test]
    public async Task ShouldReturnSameItemsInstanceOnEveryAccess() {
        var ctx = TestContext.CreateContext();

        await Assert.That(ctx.Items).IsSameReferenceAs(ctx.Items);
    }

    [Test]
    public async Task ShouldShareOneItemsInstanceBetweenRacingFirstAccesses() {
        for (var i = 0; i < 1000; i++) {
            var ctx     = TestContext.CreateContext();
            var barrier = new Barrier(2);

            var first  = Task.Run(() => { barrier.SignalAndWait(); return ctx.Items; });
            var second = Task.Run(() => { barrier.SignalAndWait(); return ctx.Items; });

            var items = await Task.WhenAll(first, second);

            await Assert.That(items[1]).IsSameReferenceAs(items[0]);
            await Assert.That(ctx.Items).IsSameReferenceAs(items[0]);
        }
    }

    [Test]
    public async Task ShouldKeepItemsAddedBeforeWrapping() {
        var ctx = TestContext.CreateContext();
        ctx.Items.AddItem("key", "value");

        var wrapped = new MessageConsumeContext<object>(ctx);

        await Assert.That(wrapped.Items).IsSameReferenceAs(ctx.Items);
        await Assert.That(wrapped.Items.GetItem<string>("key")).IsEqualTo("value");
    }
}
