using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Handler registrations that share an inferred handler type must not collapse into one.
/// </summary>
public class HandlerRegistrationTests {
    const string SubscriptionId = "handler-registration";

    [Test]
    public async Task ShouldRunAllHandlersRegisteredByFactory() {
        List<Type> handled = [];

        IReadOnlyList<Func<IServiceProvider, IEventHandler>> factories = [
            _ => new FirstHandler(handled),
            _ => new SecondHandler(handled),
            _ => new ThirdHandler(handled)
        ];

        await Handle(builder => {
                // THandler infers as IEventHandler for every call
                foreach (var factory in factories) builder.AddEventHandler(sp => factory(sp));
            }
        );

        await Assert.That(handled).IsEquivalentTo([typeof(FirstHandler), typeof(SecondHandler), typeof(ThirdHandler)]);
    }

    [Test]
    public async Task ShouldRunAllCompositionHandlersRegisteredByFactory() {
        List<Type> handled = [];

        await Handle(builder => builder
            .AddCompositionEventHandler<IEventHandler, WrappingHandler>(_ => new FirstHandler(handled), inner => new(inner))
            .AddCompositionEventHandler<IEventHandler, WrappingHandler>(_ => new SecondHandler(handled), inner => new(inner))
        );

        await Assert.That(handled).IsEquivalentTo([typeof(FirstHandler), typeof(SecondHandler)]);
    }

    [Test]
    public async Task ShouldRunAllCompositionHandlersRegisteredByFactoryWithProvider() {
        List<Type> handled = [];

        await Handle(builder => builder
            .AddCompositionEventHandler<IEventHandler, WrappingHandler>(_ => new FirstHandler(handled), (inner, _) => new(inner))
            .AddCompositionEventHandler<IEventHandler, WrappingHandler>(_ => new SecondHandler(handled), (inner, _) => new(inner))
        );

        await Assert.That(handled).IsEquivalentTo([typeof(FirstHandler), typeof(SecondHandler)]);
    }

    [Test]
    public async Task ShouldCreateFactoryHandlerOnlyOnce() {
        List<Type> handled = [];
        var        created = 0;

        var services = new ServiceCollection();
        var builder  = new TestBuilder(services, SubscriptionId);

        builder.AddEventHandler(
            _ => {
                created++;

                return new FirstHandler(handled);
            }
        );

        var provider = services.BuildServiceProvider();
        builder.Resolve(provider);
        builder.Resolve(provider);

        await Assert.That(created).IsEqualTo(1);
    }

    [Test]
    public async Task ShouldThrowWhenSameHandlerTypeRegisteredTwice() {
        var services = new ServiceCollection();

        await Assert.That(
                () => _ = services.AddSubscription<TestSub, TestOptions>(
                    SubscriptionId,
                    builder => builder.AddEventHandler<PlainHandler>().AddEventHandler<PlainHandler>()
                )
            )
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ShouldThrowWhenCompositionInnerHandlerTypeIsAlreadyRegistered() {
        var services = new ServiceCollection();

        await Assert.That(
                () => _ = services.AddSubscription<TestSub, TestOptions>(
                    SubscriptionId,
                    builder => builder
                        .AddEventHandler<PlainHandler>()
                        .AddCompositionEventHandler<PlainHandler, WrappingHandler>(inner => new(inner))
                )
            )
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ShouldAllowSameHandlerTypeInDifferentSubscriptions() {
        List<Type> handled = [];

        var services = new ServiceCollection();
        services.AddSingleton(handled);
        services.AddSubscription<TestSub, TestOptions>("sub1", builder => builder.AddEventHandler<PlainHandler>());
        services.AddSubscription<TestSub, TestOptions>("sub2", builder => builder.AddEventHandler<PlainHandler>());

        await using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetServices<TestSub>().ToArray()).HasCount(2);
    }

    static async Task Handle(Action<SubscriptionBuilder<TestSub, TestOptions>> configure) {
        var services = new ServiceCollection();
        services.AddSubscription<TestSub, TestOptions>(SubscriptionId, configure);

        await using var provider = services.BuildServiceProvider();

        var subscription = provider.GetRequiredService<TestSub>();

        await subscription.Pipe.Send(TestContext.CreateContext());
    }

    record TestOptions : SubscriptionOptions;

    class TestSub(TestOptions options, ConsumePipe consumePipe)
        : EventSubscription<TestOptions>(options, consumePipe, NullLoggerFactory.Instance, null) {
        protected override ValueTask Connect(SubscriptionRun run) => default;
    }

    sealed class TestBuilder(IServiceCollection services, string subscriptionId) : SubscriptionBuilder(services, subscriptionId) {
        public IEventHandler[] Resolve(IServiceProvider sp) => ResolveHandlers(sp);
    }

    abstract class RecordingHandler(List<Type> handled) : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            handled.Add(GetType());

            return ValueTask.FromResult(EventHandlingStatus.Success);
        }
    }

    sealed class FirstHandler(List<Type> handled) : RecordingHandler(handled);

    sealed class SecondHandler(List<Type> handled) : RecordingHandler(handled);

    sealed class ThirdHandler(List<Type> handled) : RecordingHandler(handled);

    sealed class WrappingHandler(IEventHandler inner) : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => inner.HandleEvent(context);
    }

    sealed class PlainHandler(List<Type> handled) : RecordingHandler(handled);
}
