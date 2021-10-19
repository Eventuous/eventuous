using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Monitoring;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Eventuous.Subscriptions.Tests;

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
        var handlers = subs[position].EventHandlers;

        handlers.Length.Should().Be(1);
        handlers[0].Should().BeOfType(handlerType);
    }

    [Fact]
    public void ShouldRegisterBothAsHealthReporters() {
        var services = _provider.GetServices<IReportHealth>().ToArray();
        var subs     = _provider.GetServices<TestSub>().ToArray();

        services.Length.Should().Be(2);
        services[0].Should().BeSameAs(subs[0]);
        services[1].Should().BeSameAs(subs[1]);
    }

    [Fact]
    public void ShouldRegisterBothAsHostedServices() {
        var services = _provider.GetServices<IHostedService>().ToArray();
        var subs     = _provider.GetServices<TestSub>().ToArray();

        services.Length.Should().Be(2);
        services[0].Should().BeSameAs(subs[0]);
        services[1].Should().BeSameAs(subs[1]);
    }

    static IWebHostBuilder BuildHost() => new WebHostBuilder().UseStartup<Startup>();

    class Startup {
        public static void ConfigureServices(IServiceCollection services) {
            services.AddSubscription<TestSub, TestOptions>("sub1", x => x.Field = "test")
                .AddEventHandler<Handler1>();

            services.AddSubscription<TestSub, TestOptions>("sub2")
                .AddEventHandler<Handler2>();
        }

        public void Configure(IApplicationBuilder app) { }
    }

    record TestOptions : SubscriptionOptions {
        public string? Field { get; set; }
    }

    class TestSub : EventSubscription<TestOptions> {
        public TestSub(TestOptions options, IEnumerable<IEventHandler> eventHandlers) : base(options, eventHandlers) { }

        protected override Task Subscribe(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;

        protected override Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken)
            => Task.FromResult(new EventPosition(0, DateTime.Now));
    }

    class Handler1 : IEventHandler {
        public Task HandleEvent(ReceivedEvent evt, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    class Handler2 : IEventHandler {
        public Task HandleEvent(ReceivedEvent evt, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}