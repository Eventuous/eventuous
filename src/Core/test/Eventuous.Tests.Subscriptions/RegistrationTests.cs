using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.TestHelpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eventuous.Tests.Subscriptions;

public class RegistrationTests {
    readonly IServiceProvider _provider;

    public RegistrationTests() {
        var host = new TestServer(BuildHost());
        _provider = host.Services;
    }

    [Fact]
    public void ShouldBeSingletons() {
        var subs1 = _provider.GetServices<TestSub>().ToArray();
        var subs2 = _provider.GetServices<TestSub>().ToArray();
        subs1[0].Should().BeSameAs(subs2[0]);
        subs1[1].Should().BeSameAs(subs2[1]);
    }

    [Fact]
    public void ShouldRegisterBothSubs() {
        var subs = _provider.GetServices<TestSub>().ToArray();
        subs.Length.Should().Be(2);
    }

    [Fact]
    public void SubsShouldHaveProperIds() {
        var subs = _provider.GetServices<TestSub>().ToArray();
        subs[0].Options.SubscriptionId.Should().Be("sub1");
        subs[1].Options.SubscriptionId.Should().Be("sub2");
    }

    [Theory]
    [InlineData(0, typeof(Handler1))]
    [InlineData(1, typeof(Handler2))]
    public void SubsShouldHaveHandlers(int position, Type handlerType) {
        var subs     = _provider.GetServices<TestSub>().ToArray();
        var consumer = subs[position].Pipe;
        var handlers = consumer.GetHandlers();

        handlers.Should().NotBeNull();
        handlers!.Length.Should().Be(1);
        handlers[0].Should().BeOfType(typeof(TracedEventHandler));
        var innerHandler = handlers[0].GetPrivateMember<IEventHandler>("_innerHandler");
        innerHandler.Should().BeOfType(handlerType);
    }

    [Fact]
    public void ShouldRegisterBothAsHealthReporters() {
        var services = _provider.GetServices<ISubscriptionHealth>().ToArray();
        services.Length.Should().Be(1);
    }

    [Fact]
    public void ShouldRegisterBothAsHostedServices() {
        var services = _provider.GetServices<IHostedService>()
            .Where(x => x is SubscriptionHostedService)
            .ToArray();

        var subs   = _provider.GetServices<TestSub>().ToArray();
        var health = _provider.GetRequiredService<ISubscriptionHealth>();

        services.Length.Should().Be(2);

        // Should have one sub each
        services[0]
            .GetPrivateMember<IMessageSubscription>("_subscription")
            .Should()
            .BeSameAs(subs[0]);

        services[1]
            .GetPrivateMember<IMessageSubscription>("_subscription")
            .Should()
            .BeSameAs(subs[1]);

        // Should have the same health check
        services[0]
            .GetPrivateMember<ISubscriptionHealth>("_subscriptionHealth")
            .Should()
            .BeSameAs(health);

        services[1]
            .GetPrivateMember<ISubscriptionHealth>("_subscriptionHealth")
            .Should()
            .BeSameAs(health);
    }

    [Fact]
    public void ShouldRegisterTwoMeasures() {
        var subs    = _provider.GetServices<TestSub>().ToArray();
        var measure = _provider.GetRequiredService<SubscriptionMetrics>();
    }

    static IWebHostBuilder BuildHost() => new WebHostBuilder().UseStartup<Startup>();

    class Startup {
        public static void ConfigureServices(IServiceCollection services) {
            services.AddSubscription<TestSub, TestOptions>(
                "sub1",
                builder => builder
                    .Configure(x => x.Field = "test")
                    .AddEventHandler<Handler1>()
            );

            services.AddSubscription<TestSub, TestOptions>(
                "sub2",
                builder => builder
                    .AddEventHandler<Handler2>()
            );

            services.AddOpenTelemetryMetrics(builder => builder.AddEventuousSubscriptions());
        }

        public void Configure(IApplicationBuilder app) { }
    }

    record TestOptions : SubscriptionOptions {
        public string? Field { get; set; }
    }

    class TestSub : EventSubscription<TestOptions>, IMeasuredSubscription {
        public TestSub(TestOptions options, ConsumePipe consumePipe) : base(
            options,
            consumePipe,
            NullLoggerFactory.Instance
        ) { }

        protected override ValueTask Subscribe(CancellationToken cancellationToken) => default;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;

        public GetSubscriptionGap GetMeasure()
            => _
                => new ValueTask<SubscriptionGap>(
                    new SubscriptionGap(SubscriptionId, 0, TimeSpan.Zero)
                );
    }

    class Handler1 : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext evt) => default;
    }

    class Handler2 : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext evt) => default;
    }
}
