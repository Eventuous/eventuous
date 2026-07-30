using Eventuous.Gateway;
using Eventuous.Producers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable ClassNeverInstantiated.Local

namespace Eventuous.Tests.Gateway;

public class RegistrationTests {
    [Test]
    public async Task Test() {
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .UseStartup<Startup>()
            )
            .Build();
        await host.StartAsync();

        var testServer = host.GetTestServer();
        testServer.Dispose();
    }

    class Startup {
        public static void ConfigureServices(IServiceCollection services) {
            services.AddGateway<TestSub, TestOptions, TestProducer, TestProduceOptions>("shovel1", RouteAndTransform);
            services.AddGateway<TestSub, TestOptions, TestProducer, TestProduceOptions, TestTransform>("shovel2");
        }

        static ValueTask<GatewayMessage<TestProduceOptions>[]> RouteAndTransform(object message) => new();

        public static void Configure(IApplicationBuilder app) { }
    }

    class TestTransform : IGatewayTransform<TestProduceOptions> {
        public ValueTask<GatewayMessage<TestProduceOptions>[]> RouteAndTransform(IMessageConsumeContext context) => new();
    }

    record TestOptions : SubscriptionOptions;

    class TestSub(TestOptions options, ConsumePipe consumePipe) : EventSubscription<TestOptions>(options, consumePipe, NullLoggerFactory.Instance, null) {
        protected override ValueTask Subscribe(CancellationToken cancellationToken) => default;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;
    }

    class TestProducer : BaseProducer<TestProduceOptions> {
        // ReSharper disable once CollectionNeverQueried.Local
        // ReSharper disable once MemberCanBePrivate.Local
        public List<ProducedMessage> ProducedMessages { get; } = [];

        protected override Task ProduceMessages(
                StreamName                   stream,
                IEnumerable<ProducedMessage> messages,
                TestProduceOptions?          options,
                CancellationToken            cancellationToken = default
            ) {
            ProducedMessages.AddRange(messages);

            return Task.CompletedTask;
        }
    }

    record TestProduceOptions;
}
