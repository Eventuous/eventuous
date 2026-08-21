using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// The subscription disposes the handlers its builder created. Handlers owned by the container or supplied by
/// the caller are left alone.
/// </summary>
public class HandlerDisposalTests {
    const string SubscriptionId = "handler-disposal";

    [Test]
    public async Task ShouldDisposeHandlerCreatedByFactory() {
        DisposableHandler? handler = null;

        var resolved = Resolve(builder => builder.AddEventHandler(_ => handler = new()));
        await resolved.Subscription.DisposeAsync();

        await Assert.That(handler!.Disposals).IsEqualTo(1);
    }

    [Test]
    public async Task ShouldDisposeAsyncHandlerCreatedByFactory() {
        AsyncDisposableHandler? handler = null;

        var resolved = Resolve(builder => builder.AddEventHandler(_ => handler = new()));
        await resolved.Subscription.DisposeAsync();

        await Assert.That(handler!.Disposals).IsEqualTo(1);
    }

    [Test]
    public async Task ShouldPreferAsyncDisposal() {
        DoublyDisposableHandler? handler = null;

        var resolved = Resolve(builder => builder.AddEventHandler(_ => handler = new()));
        await resolved.Subscription.DisposeAsync();

        await Assert.That(handler!.AsyncDisposals).IsEqualTo(1);
        await Assert.That(handler.Disposals).IsEqualTo(0);
    }

    [Test]
    public async Task ShouldDisposeInnerCompositionHandlerCreatedByFactory() {
        DisposableHandler?         inner   = null;
        DisposableWrappingHandler? wrapper = null;

        var resolved = Resolve(
            builder => builder.AddCompositionEventHandler<DisposableHandler, DisposableWrappingHandler>(
                _ => inner = new(),
                handler => wrapper = new(handler)
            )
        );
        await resolved.Subscription.DisposeAsync();

        await Assert.That(inner!.Disposals).IsEqualTo(1);
        // The wrapping handler decorates the inner one, it holds nothing of its own
        await Assert.That(wrapper!.Disposals).IsEqualTo(0);
    }

    [Test]
    public async Task ShouldNotDisposeCompositionOfContainerOwnedInnerHandler() {
        DisposableWrappingHandler? wrapper = null;

        var resolved = Resolve(
            builder => builder.AddCompositionEventHandler<DisposableHandler, DisposableWrappingHandler>(handler => wrapper = new(handler))
        );
        var inner = resolved.Provider.GetRequiredKeyedService<DisposableHandler>(SubscriptionId);
        await resolved.Subscription.DisposeAsync();

        await Assert.That(wrapper!.Disposals).IsEqualTo(0);
        await Assert.That(inner.Disposals).IsEqualTo(0);
    }

    [Test]
    public async Task ShouldNotDisposeHandlerOwnedByContainer() {
        var resolved = Resolve(builder => builder.AddEventHandler<DisposableHandler>());
        var handler  = resolved.Provider.GetRequiredKeyedService<DisposableHandler>(SubscriptionId);

        await resolved.Subscription.DisposeAsync();

        await Assert.That(handler.Disposals).IsEqualTo(0);
    }

    [Test]
    public async Task ShouldNotDisposeHandlerSuppliedByCaller() {
        var handler = new DisposableHandler();

        var resolved = Resolve(builder => builder.AddEventHandler(handler));
        await resolved.Subscription.DisposeAsync();

        await Assert.That(handler.Disposals).IsEqualTo(0);
    }

    [Test]
    public async Task ShouldDisposeHandlerOnlyOnce() {
        DisposableHandler? handler = null;

        var resolved = Resolve(builder => builder.AddEventHandler(_ => handler = new()));
        await resolved.Subscription.DisposeAsync();
        await resolved.Subscription.DisposeAsync();

        await Assert.That(handler!.Disposals).IsEqualTo(1);
    }

    [Test]
    public async Task ShouldDisposeFiltersBeforeHandlers() {
        List<string> order = [];

        var resolved = Resolve(
            builder => builder
                .AddConsumeFilterFirst(new RecordingFilter(order))
                .AddEventHandler(_ => new RecordingHandler(order))
        );
        await resolved.Subscription.DisposeAsync();

        await Assert.That(order).IsEquivalentTo(["filter", "handler"]);
    }

    static Resolved Resolve(Action<SubscriptionBuilder<TestSub, TestOptions>> configure) {
        var services = new ServiceCollection();
        services.AddSubscription<TestSub, TestOptions>(SubscriptionId, configure);

        var provider = services.BuildServiceProvider();

        return new(provider, provider.GetRequiredService<TestSub>());
    }

    record Resolved(ServiceProvider Provider, TestSub Subscription);

    record TestOptions : SubscriptionOptions;

    class TestSub(TestOptions options, ConsumePipe consumePipe)
        : EventSubscription<TestOptions>(options, consumePipe, NullLoggerFactory.Instance, null) {
        protected override ValueTask Connect(SubscriptionRun run) => default;
    }

    class SucceedingHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => ValueTask.FromResult(EventHandlingStatus.Success);
    }

    sealed class DisposableHandler : SucceedingHandler, IDisposable {
        public int Disposals { get; private set; }

        public void Dispose() => Disposals++;
    }

    sealed class AsyncDisposableHandler : SucceedingHandler, IAsyncDisposable {
        public int Disposals { get; private set; }

        public ValueTask DisposeAsync() {
            Disposals++;

            return default;
        }
    }

    sealed class DoublyDisposableHandler : SucceedingHandler, IDisposable, IAsyncDisposable {
        public int Disposals      { get; private set; }
        public int AsyncDisposals { get; private set; }

        public void Dispose() => Disposals++;

        public ValueTask DisposeAsync() {
            AsyncDisposals++;

            return default;
        }
    }

    sealed class DisposableWrappingHandler(IEventHandler inner) : SucceedingHandler, IDisposable {
        public IEventHandler Inner     { get; }      = inner;
        public int           Disposals { get; private set; }

        public void Dispose() => Disposals++;
    }

    sealed class RecordingHandler(List<string> order) : SucceedingHandler, IDisposable {
        public void Dispose() => order.Add("handler");
    }

    sealed class RecordingFilter(List<string> order) : ConsumeFilter<IMessageConsumeContext>, IAsyncDisposable {
        protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next)
            => next?.Value.Send(context, next.Next) ?? default;

        public ValueTask DisposeAsync() {
            order.Add("filter");

            return default;
        }
    }
}
