using Eventuous.Producers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Shovel.Tests;

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
            services.AddShovel<TestSub, TestOptions, TestProducer, TestProduceOptions>(
                "shovel",
                RouteAndTransform
            );

            services.AddSubscription<TestSub, TestOptions>(
                "sub1",
                builder => builder.AddEventHandler<Handler>()
            );
        }

        static ValueTask<ShovelContext<TestProduceOptions>?> RouteAndTransform(object message) {
            throw new NotImplementedException();
        }

        public void Configure(IApplicationBuilder app) { }
    }

    record TestOptions : SubscriptionOptions;

    class TestSub : EventSubscription<TestOptions> {
        public TestSub(
            TestOptions      options,
            IMessageConsumer consumer
        ) : base(options, consumer) { }

        protected override ValueTask Subscribe(CancellationToken cancellationToken) => default;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;
    }

    class Handler : IEventHandler {
        public ValueTask HandleEvent(IMessageConsumeContext evt, CancellationToken cancellationToken)
            => default;
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
    }

    record TestProduceOptions { }
}