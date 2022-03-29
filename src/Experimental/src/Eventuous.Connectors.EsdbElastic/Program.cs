using Eventuous.Connectors.Base;
using Eventuous.Connectors.EsdbElastic.Config;
using Eventuous.Connectors.EsdbElastic.Conversions;
using Eventuous.Connectors.EsdbElastic.Index;
using Eventuous.Connectors.EsdbElastic.Infrastructure;
using Eventuous.ElasticSearch.Producers;
using Eventuous.ElasticSearch.Projections;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Registrations;
using Nest;

TypeMap.RegisterKnownEventTypes();
var builder = WebApplication.CreateBuilder();
builder.AddConfiguration();

var config = builder.Configuration.GetConnectorConfig<EsdbConfig, ElasticConfig>();

builder.ConfigureSerilog();

builder.Services.AddSingleton(config.Target.DataStream);

var serializer = new RawDataSerializer();

builder.Services
    .AddSingleton<IEventSerializer>(serializer)
    .AddEventStoreClient(Ensure.NotEmptyString(config.Source.ConnectionString, "EventStoreDB connection string"))
    .AddElasticClient(
        Ensure.NotEmptyString(config.Target.ConnectionString, "ElasticSearch connection string"),
        config.Target.ApiKey
    );

var concurrencyLimit = config.Source.ConcurrencyLimit;
var indexName        = config.Target.DataStream.IndexName;

new ConnectorBuilder()
    .SubscribeWith<AllStreamSubscription, AllStreamSubscriptionOptions>(
        Ensure.NotEmptyString(config.ConnectorId, "ConnectorId")
    )
    .ConfigureSubscriptionOptions(
        cfg => {
            cfg.EventSerializer  = serializer;
            cfg.ConcurrencyLimit = concurrencyLimit;
        }
    )
    .ConfigureSubscription(
        b => {
            b.UseCheckpointStore<ElasticCheckpointStore>();
            b.WithPartitioningByStream(concurrencyLimit);
        }
    )
    .ProduceWith<ElasticProducer, ElasticProduceOptions>()
    .TransformWith(_ => new EventTransform(indexName))
    .Register(builder.Services);

builder.AddStartupJob<IElasticClient, IndexConfig>(SetupIndex.CreateIfNecessary);

await builder.GetHost().RunConnector();
