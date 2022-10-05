using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eventuous.Gateway.Tests;

public class RegistrationTestsWithOptions {
    readonly IServiceProvider _provider;

    public RegistrationTestsWithOptions() {
        var host = new TestServer(BuildHost());
        _provider = host.Services;
    }

    [Fact]
    public void Test() { }

    static IWebHostBuilder BuildHost() => new WebHostBuilder().UseStartup<Startup>();

    class Startup {
        public static void ConfigureServices(IServiceCollection services) {
            services.AddGateway<TestSub, TestOptions, TestProducer, TestProduceOptions>("shovel1", RouteAndTransform);
            services.AddGateway<TestSub, TestOptions, TestProducer, TestProduceOptions, TestTransform>("shovel2");
        }

        static ValueTask<GatewayMessage<TestProduceOptions>[]> RouteAndTransform(object message) => new();

        public void Configure(IApplicationBuilder app) { }
    }

    class TestTransform : IGatewayTransform<TestProduceOptions> {
        public ValueTask<GatewayMessage<TestProduceOptions>[]> RouteAndTransform(IMessageConsumeContext context)
            => new();
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

    class TestProducer : BaseProducer<TestProduceOptions> {
        public List<ProducedMessage> ProducedMessages { get; } = new();

        protected override Task ProduceMessages(
            StreamName                   stream,
            IEnumerable<ProducedMessage> messages,
            TestProduceOptions?          options,
            CancellationToken            cancellationToken = default
        ) {
            ProducedMessages.AddRange(messages);
            return Task.CompletedTask;
        }

        public TestProducer() : base() { }
    }

    record TestProduceOptions { }
}
