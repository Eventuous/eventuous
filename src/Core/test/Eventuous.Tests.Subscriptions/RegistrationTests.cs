using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

// ReSharper disable ClassNeverInstantiated.Local

namespace Eventuous.Tests.Subscriptions;

public class RegistrationTests() {
    readonly TestServer     _server = new(BuildHost());
    readonly Fixture        _auto   = new();
    readonly ILoggerFactory _logger = LoggingExtensions.GetLoggerFactory();

    [Test]
    public void ShouldBeSingletons() {
        var subs1 = _server.Services.GetServices<TestSub>().ToArray();
        var subs2 = _server.Services.GetServices<TestSub>().ToArray();
        subs1[0].Should().BeSameAs(subs2[0]);
        subs1[1].Should().BeSameAs(subs2[1]);
    }

    [Test]
    public void ShouldRegisterBothSubs() {
        var subs = _server.Services.GetServices<TestSub>().ToArray();
        subs.Length.Should().Be(2);
    }

    [Test]
    public void SubsShouldHaveProperIds() {
        var subs = _server.Services.GetServices<TestSub>().ToArray();
        subs[0].Options.SubscriptionId.Should().Be("sub1");
        subs[1].Options.SubscriptionId.Should().Be("sub2");
    }

    [Test]
    [Arguments(0, typeof(Handler1))]
    [Arguments(1, typeof(Handler2))]
    public async Task SubsShouldHaveHandlers(int position, Type handlerType) {
        var subs    = _server.Services.GetServices<TestSub>().ToArray();
        var logger  = _server.Services.GetRequiredService<TestHandlerLogger>();
        var current = subs[position];

        var ctx = new MessageConsumeContext(
            _auto.Create<string>(),
            _auto.Create<string>(),
            _auto.Create<string>(),
            _auto.Create<string>(),
            0,
            0,
            0,
            0,
            DateTime.UtcNow,
            new TestEvent(),
            new Metadata(),
            current.SubscriptionId,
            default
        ) { LogContext = new(current.SubscriptionId, _logger) };
        await current.Pipe.Send(ctx);

        var handled = logger.Records.Where(x => x.Context.SubscriptionId == current.SubscriptionId).ToArray();
        handled.Length.Should().Be(1);
        handled[0].HandlerType.Should().Be(handlerType);
        handled[0].Context.MessageId.Should().Be(ctx.MessageId);
        handled[0].Context.MessageType.Should().Be(ctx.MessageType);
    }

    [Test]
    public void ShouldRegisterBothAsHealthReporters() {
        var services = _server.Services.GetServices<ISubscriptionHealth>().ToArray();
        var health   = _server.Services.GetServices<SubscriptionHealthCheck>().ToArray();

        services.Length.Should().Be(1);
        health.Length.Should().Be(1);
        services.Single().Should().BeSameAs(health.Single());
    }

    [Test]
    public async Task BothShouldBeRunningAndReportHealthy(CancellationToken cancellationToken) {
        var subs   = _server.Services.GetServices<TestSub>().ToArray();
        var health = _server.Services.GetRequiredService<ISubscriptionHealth>() as SubscriptionHealthCheck;

        subs.Length.Should().Be(2);
        subs.Should().AllSatisfy(x => x.IsRunning.Should().BeTrue());

        health.Should().NotBeNull();
        var check = await health!.CheckHealthAsync(new(), cancellationToken);
        check.Data["sub1"].Should().Be("Healthy");
        check.Data["sub2"].Should().Be("Healthy");
        check.Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public void ShouldRegisterTwoMeasures() {
        var subs = _server.Services.GetServices<TestSub>().ToArray();
        subs.Should().NotBeEmpty();
        _server.Services.GetRequiredService<SubscriptionMetrics>();
    }

    static IWebHostBuilder BuildHost() => new WebHostBuilder().UseStartup<Startup>();

    class Startup {
        public static void ConfigureServices(IServiceCollection services) {
            services.AddSingleton(new TestHandlerLogger());

            services.AddSubscription<TestSub, TestOptions>(
                "sub1",
                builder => builder
                    .Configure(x => x.Field = "test")
                    .AddEventHandler<Handler1>()
            );

            services.AddSubscription<TestSub, TestOptions>("sub2", builder => builder.AddEventHandler<Handler2>());
            services.AddOpenTelemetry().WithMetrics(builder => builder.AddEventuousSubscriptions());
            services.AddHealthChecks().AddSubscriptionsHealthCheck("subscriptions", HealthStatus.Unhealthy, ["tag"]);
        }

        public void Configure(IApplicationBuilder app) { }
    }

    record TestOptions : SubscriptionOptions {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string? Field { get; set; }
    }

    class TestSub(TestOptions options, ConsumePipe consumePipe)
        : EventSubscription<TestOptions>(options, consumePipe, NullLoggerFactory.Instance, null), IMeasuredSubscription {
        protected override ValueTask Subscribe(CancellationToken cancellationToken) => default;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;

        public GetSubscriptionEndOfStream GetMeasure() => _ => new(new EndOfStream(SubscriptionId, 0, DateTime.UtcNow));
    }

    public abstract class BaseTestHandler(TestHandlerLogger logger) : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) => logger.EventReceived(GetType(), ctx);
    }

    public class Handler1(TestHandlerLogger logger) : BaseTestHandler(logger);

    public class Handler2(TestHandlerLogger logger) : BaseTestHandler(logger);

    record TestEvent;
}

public class TestHandlerLogger {
    public ValueTask<EventHandlingStatus> EventReceived(Type handlerType, IMessageConsumeContext ctx) {
        Records.Add(new(handlerType, ctx));

        return ValueTask.FromResult(EventHandlingStatus.Success);
    }

    public List<TestHandlerLogRecord> Records { get; } = [];
}

public record TestHandlerLogRecord(Type HandlerType, IMessageConsumeContext Context);
