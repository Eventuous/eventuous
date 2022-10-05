using Eventuous.Producers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eventuous.Gateway.Tests;

public class RegistrationTests {
    readonly IServiceProvider _provider;

    public RegistrationTests() {
        var host = new TestServer(BuildHost());
        _provider = host.Services;
    }

    [Fact]
    public void Test() { }

    static IWebHostBuilder BuildHost() => new WebHostBuilder().UseStartup<Startup>();

    class Startup {
        public static void ConfigureServices(IServiceCollection services) {
            services.AddGateway<TestSub, TestOptions, TestProducer>("shovel1", RouteAndTransform);
            services.AddGateway<TestSub, TestOptions, TestProducer, TestTransform>("shovel2");
        }

        static ValueTask<GatewayMessage[]> RouteAndTransform(object message) => new();

        public void Configure(IApplicationBuilder app) { }
    }

    class TestTransform : IGatewayTransform {
        public ValueTask<GatewayMessage[]> RouteAndTransform(IMessageConsumeContext context) => new();
    }

    record TestOptions : SubscriptionOptions;

    class TestSub : EventSubscription<TestOptions> {
        public TestSub(TestOptions options, ConsumePipe consumePipe) : base(
            options,
            consumePipe,
            NullLoggerFactory.Instance
        ) { }

        protected override ValueTask Subscribe(CancellationToken cancellationToken) => default;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;
    }

    class Handler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) => default;
    }

    class TestProducer : BaseProducer {
        public List<ProducedMessage> ProducedMessages { get; } = new();

        protected override Task ProduceMessages(
            StreamName                   stream,
            IEnumerable<ProducedMessage> messages,
            CancellationToken            cancellationToken = default
        ) {
            ProducedMessages.AddRange(messages);
            return Task.CompletedTask;
        }

        public TestProducer() : base() { }
    }
}
