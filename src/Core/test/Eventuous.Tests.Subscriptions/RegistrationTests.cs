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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

// ReSharper disable ClassNeverInstantiated.Local

namespace Eventuous.Tests.Subscriptions;

public class RegistrationTests {
    TestServer _server = null!;
    IHost      _host    = null!;

    readonly ILoggerFactory _logger = LoggingExtensions.GetLoggerFactory();

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
    public void ShouldBeSingletons() {
        var subs1 = _server.Services.GetServices<TestSub>().ToArray();
        var subs2 = _server.Services.GetServices<TestSub>().ToArray();
        subs1[0].ShouldBeSameAs(subs2[0]);
        subs1[1].ShouldBeSameAs(subs2[1]);
    }

    [Test]
    public void ShouldRegisterBothSubs() {
        var subs = _server.Services.GetServices<TestSub>().ToArray();
        subs.Length.ShouldBe(2);
    }

    [Test]
    public void SubsShouldHaveProperIds() {
        var subs = _server.Services.GetServices<TestSub>().ToArray();
        subs[0].Options.SubscriptionId.ShouldBe("sub1");
        subs[1].Options.SubscriptionId.ShouldBe("sub2");
    }

    [Test]
    [Arguments(0, typeof(Handler1))]
    [Arguments(1, typeof(Handler2))]
    public async Task SubsShouldHaveHandlers(int position, Type handlerType) {
        var subs    = _server.Services.GetServices<TestSub>().ToArray();
        var logger  = _server.Services.GetRequiredService<TestHandlerLogger>();
        var current = subs[position];

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
            current.SubscriptionId,
            default
        ) { LogContext = new(current.SubscriptionId, _logger) };
        await current.Pipe.Send(ctx);

        var handled = logger.Records.Where(x => x.Context.SubscriptionId == current.SubscriptionId).ToArray();
        handled.Length.ShouldBe(1);
        handled[0].HandlerType.ShouldBe(handlerType);
        handled[0].Context.MessageId.ShouldBe(ctx.MessageId);
        handled[0].Context.MessageType.ShouldBe(ctx.MessageType);
    }

    [Test]
    public void ShouldRegisterBothAsHealthReporters() {
        var services = _server.Services.GetServices<ISubscriptionHealth>().ToArray();
        var health   = _server.Services.GetServices<SubscriptionHealthCheck>().ToArray();

        services.Length.ShouldBe(1);
        health.Length.ShouldBe(1);
        services.Single().ShouldBeSameAs(health.Single());
    }

    [Test]
    public async Task BothShouldBeRunningAndReportHealthy(CancellationToken cancellationToken) {
        var subs   = _server.Services.GetServices<TestSub>().ToArray();
        var health = _server.Services.GetRequiredService<ISubscriptionHealth>() as SubscriptionHealthCheck;

        subs.Length.ShouldBe(2);
        subs.ShouldAllBe(x => x.IsRunning);

        health.ShouldNotBeNull();
        var check = await health.CheckHealthAsync(new(), cancellationToken);
        check.Data["sub1"].ShouldBe("Healthy");
        check.Data["sub2"].ShouldBe("Healthy");
        check.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Test]
    public void ShouldRegisterTwoMeasures() {
        var subs = _server.Services.GetServices<TestSub>().ToArray();
        subs.ShouldNotBeEmpty();
        _server.Services.GetRequiredService<SubscriptionMetrics>();
    }

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

        public static void Configure(IApplicationBuilder app) { }
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
