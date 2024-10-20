using Bookings.Infrastructure;
using Bookings.Payments;
using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.Postgresql;
using Npgsql;
using Serilog;

TypeMap.RegisterKnownEventTypes();
Logging.ConfigureLog();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// OpenTelemetry instrumentation must be added before adding Eventuous services
builder.Services.AddTelemetry();
builder.Services.AddEventuous(builder.Configuration);

var app = builder.Build();

app.Services.AddEventuousLogs();
app.UseSwagger().UseSwaggerUI();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Here we discover commands by their annotations
app.MapDiscoveredCommands<PaymentState>();

if (app.Configuration.GetValue<bool>("Postgres:InitializeDatabase")) {
    await InitialiseSchema(app);
}

try {
    app.Run("http://*:5052");

    return 0;
} catch (Exception e) {
    Log.Fatal(e, "Host terminated unexpectedly");

    return 1;
} finally {
    Log.CloseAndFlush();
}

async Task InitialiseSchema(IHost webApplication) {
    var store  = webApplication.Services.GetRequiredService<PostgresStore>();
    var schema = store.Schema;
    var ds     = webApplication.Services.GetRequiredService<NpgsqlDataSource>();
    await schema.CreateSchema(ds, webApplication.Services.GetRequiredService<ILogger<Schema>>());
}
