using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

public class CompositionHandlerTests {
    TestServer _server = null!;
    IHost      _host   = null!;

    [Before(Test)]
    public async Task Setup() {
        _host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .UseStartup<Startup>()
            )
            .Build();
        await _host.StartAsync();

        _server = _host.GetTestServer();
    }

    [After(Test)]
    public async Task Teardown() {
        _server.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Test]
    public void ShouldResolveCompositionHandlerWithFactory() {
        // This test validates that AddCompositionEventHandler correctly registers
        // handlers when using a factory function
        var handler = _server.Services.GetRequiredKeyedService<TestHandler>("sub-with-factory");
        handler.ShouldNotBeNull();
        handler.Dependency.ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldHandleEventWithCompositionHandler() {
        var logger = _server.Services.GetRequiredService<TestHandlerLogger>();
        var subs   = _server.Services.GetServices<TestSub>().ToArray();
        var sub    = subs.FirstOrDefault(x => x.SubscriptionId == "sub-with-factory");

        sub.ShouldNotBeNull();

        var ctx = new MessageConsumeContext(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            0,
            0,
            0,
            0,
            DateTime.UtcNow,
            new TestEvent(),
            new(),
            sub.SubscriptionId,
            default
        ) { LogContext = new(sub.SubscriptionId, NullLoggerFactory.Instance) };

        await sub.Pipe.Send(ctx);

        var handled = logger.Records.Where(x => x.Context.SubscriptionId == sub.SubscriptionId).ToArray();
        handled.Length.ShouldBe(1);
        handled[0].HandlerType.ShouldBe(typeof(CompositionWrapper));
        handled[0].Context.MessageId.ShouldBe(ctx.MessageId);
    }

    class Startup {
        public static void ConfigureServices(IServiceCollection services) {
            services.AddSingleton(new TestHandlerLogger());
            services.AddSingleton<TestDependency>();

            // Test the AddCompositionEventHandler with a factory function
            services.AddSubscription<TestSub, TestOptions>(
                "sub-with-factory",
                builder => builder.AddCompositionEventHandler<TestHandler, CompositionWrapper>(
                    sp => new TestHandler(sp.GetRequiredService<TestDependency>(), sp.GetRequiredService<TestHandlerLogger>()),
                    handler => new CompositionWrapper(handler, sp => sp.GetRequiredService<TestHandlerLogger>())
                )
            );
        }

        public void Configure(IApplicationBuilder app) { }
    }

    record TestOptions : SubscriptionOptions;

    class TestSub(TestOptions options, ConsumePipe consumePipe)
        : EventSubscription<TestOptions>(options, consumePipe, NullLoggerFactory.Instance, null) {
        protected override ValueTask Subscribe(CancellationToken cancellationToken) => default;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;
    }

    public class TestDependency {
        public string Value { get; } = "test-value";
    }

    public class TestHandler(TestDependency dependency, TestHandlerLogger logger) : BaseEventHandler {
        public TestDependency Dependency { get; } = dependency;

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) 
            => logger.EventReceived(GetType(), ctx);
    }

    public class CompositionWrapper(IEventHandler innerHandler, Func<IServiceProvider, TestHandlerLogger> getLogger) : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) {
            // Wrap the inner handler call - this simulates what PollyEventHandler does
            return getLogger(ctx.Services).EventReceived(GetType(), ctx);
        }
    }

    record TestEvent;
}
