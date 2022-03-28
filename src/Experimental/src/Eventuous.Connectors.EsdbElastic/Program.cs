using Eventuous.AspNetCore;
using Eventuous.Connectors.EsdbElastic;
using Eventuous.Connectors.EsdbElastic.Index;
using Eventuous.Connectors.EsdbElastic.Infrastructure;
using Eventuous.ElasticSearch.Producers;
using Eventuous.ElasticSearch.Projections;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Nest;
using Serilog;
using Serilog.Events;

TypeMap.RegisterKnownEventTypes();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Grpc", LogEventLevel.Information)
    .MinimumLevel.Override("Grpc.Net.Client.Internal.GrpcCall", LogEventLevel.Error)
    .MinimumLevel.Override("Microsoft.AspNetCore.Mvc.Infrastructure", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = Host.CreateDefaultBuilder(args);
builder.UseSerilog();

builder.ConfigureServices(
    (ctx, services) => {
        var serializer = new StringSerializer();
        services.AddSingleton<IEventSerializer>(serializer);
        services.AddEventStoreClient("esdb://localhost:2113?tls=false");
        services.AddElasticClient(ctx.Configuration["Elastic:ConnectionString"], null);
        services.AddSingleton(new EventTransform("eventlog"));
        services.AddEventProducer<ElasticProducer>();

        services
            .AddCheckpointStore<ElasticCheckpointStore>()
            .AddGateway<AllStreamSubscription, AllStreamSubscriptionOptions,
                ElasticProducer, ElasticProduceOptions,
                EventTransform>("esdb-elastic-connector", cfg => cfg.EventSerializer = serializer);
    }
);

var host = builder.Build();

var config = new IndexConfig(
    new DataStreamTemplateConfig("eventlog-template", "eventlog"),
    new LifecycleConfig(
        "eventlog-policy",
        new TierDefinition[] {
            new("hot") {
                MinAge   = "1d",
                Priority = 100,
                Rollover = new Rollover("1d", null, "100mb")
            },
            new("warm") {
                MinAge     = "1d",
                Priority   = 50,
                ForceMerge = new ForceMerge(1)
            },
            new("cold") {
                MinAge   = "1d",
                Priority = 0,
                ReadOnly = true
            }
        }
    )
);

var client = host.Services.GetRequiredService<IElasticClient>();
await SetupIndex.CreateIfNecessary(client, config);

host.AddEventuousLogs();
await host.RunAsync();
