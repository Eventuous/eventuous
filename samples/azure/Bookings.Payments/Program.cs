using Bookings.Infrastructure;
using Bookings.Payments;
using Bookings.Payments.Domain;
using Eventuous;
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
app.UseSwagger();
app.UseSwaggerUI();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Here we discover commands by their annotations
app.MapDiscoveredCommands<PaymentState>();

try {
    app.Run();

    return 0;
} catch (Exception e) {
    Log.Fatal(e, "Host terminated unexpectedly");

    return 1;
} finally {
    Log.CloseAndFlush();
}
