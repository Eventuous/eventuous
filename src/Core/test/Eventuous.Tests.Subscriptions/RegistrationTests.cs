using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        var consumer = subs[position].Consumer;

        // handlers.Length.Should().Be(1);
        // handlers[0].Should().BeOfType(handlerType);
    }

    [Fact]
    public void ShouldRegisterBothAsHealthReporters() {
        var services = _provider.GetServices<ISubscriptionHealth>().ToArray();
        var subs     = _provider.GetServices<TestSub>().ToArray();

        services.Length.Should().Be(1);
        // services[0].Should().BeSameAs(subs[0]);
        // services[1].Should().BeSameAs(subs[1]);
        // Need to test the reporting
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
        public TestSub(TestOptions options, IMessageConsumer consumer) : base(options, consumer) { }

        protected override ValueTask Subscribe(CancellationToken cancellationToken) => default;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;
    }

    class Handler1 : IEventHandler {
        public Task HandleEvent(IMessageConsumeContext evt, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    class Handler2 : IEventHandler {
        public Task HandleEvent(IMessageConsumeContext evt, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}