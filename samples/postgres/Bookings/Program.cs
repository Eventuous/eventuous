using Bookings;
using Bookings.Domain.Bookings;
using Bookings.Infrastructure;
using Eventuous;
using Eventuous.Postgresql;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;
using Serilog;

TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.V1.RoomBooked).Assembly);
Logging.ConfigureLog();

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Trace).AddConsole();
builder.Host.UseSerilog();

builder.Services
    .AddControllers()
    .AddJsonOptions(cfg => cfg.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTelemetry();
builder.Services.AddEventuous(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Postgres:InitializeDatabase")) {
    await InitialiseSchema(app);
}

app.UseSerilogRequestLogging();
app.UseEventuousLogs();
app.UseSwagger().UseSwaggerUI();
app.MapControllers();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

try {
    app.Run("http://*:5051");

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
    var log    = webApplication.Services.GetRequiredService<ILogger<Schema>>();
    await schema.CreateSchema(ds, log);
}
