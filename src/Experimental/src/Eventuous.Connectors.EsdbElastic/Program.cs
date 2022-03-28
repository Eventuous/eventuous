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

builder.ConfigureAppConfiguration(
    cfg => cfg.AddYamlFile("config.yaml", false, true).AddEnvironmentVariables()
);

builder.ConfigureServices(
    (ctx, services) => {
        var config     = ctx.Configuration.GetSection("elastic").Get<ElasticConfig>();
        services.AddSingleton(config.DataStream);
        
        var serializer = new StringSerializer();
        services.AddSingleton<IEventSerializer>(serializer);
        services.AddEventStoreClient(ctx.Configuration["eventstoredb:connectionString"]);
        services.AddElasticClient(config.ConnectionString!, config.ApiKey);
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

var client      = host.Services.GetRequiredService<IElasticClient>();
var indexConfig = host.Services.GetRequiredService<IndexConfig>();
await SetupIndex.CreateIfNecessary(client, indexConfig);

host.AddEventuousLogs();
await host.RunAsync();
