using Eventuous.Connectors.EsdbElastic;
using Eventuous.ElasticSearch.Producers;
using Eventuous.ElasticSearch.Projections;
using Eventuous.EventStore.Subscriptions;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(
    services => {
        services
            .AddCheckpointStore<ElasticCheckpointStore>()
            .AddGateway<AllStreamSubscription, AllStreamSubscriptionOptions,
                ElasticProducer, ElasticProduceOptions,
                Transform>("esdb-elastic-connector");
    }
);

var host = builder.Build();

await host.RunAsync();