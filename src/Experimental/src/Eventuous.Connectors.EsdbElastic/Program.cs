using Elasticsearch.Net;
using Eventuous.Connectors.Base;
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

var config  = builder.Configuration.GetSection("elastic").Get<ElasticConfig>();

builder.ConfigureSerilog();

builder.Services.AddSingleton(config.DataStream);

var serializer = new RawDataSerializer();

builder.Services
    .AddSingleton<IEventSerializer>(serializer)
    .AddEventStoreClient(builder.Configuration["eventstoredb:connectionString"])
    .AddElasticClient(config.ConnectionString!, config.ApiKey);

var concurrencyLimit = builder.Configuration.GetValue<uint>("connector:concurrencyLimit", 1);

new ConnectorBuilder()
    .SubscribeWith<AllStreamSubscription, AllStreamSubscriptionOptions>("esdb-elastic-connector")
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
    .TransformWith(_ => new EventTransform("eventlog"))
    .Register(builder.Services);

builder.AddStartupJob<IElasticClient, IndexConfig>(SetupIndex.CreateIfNecessary);

await builder.GetHost().RunConnector();
