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
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Bookings Payments API", Version = "v1" }));
// OpenTelemetry instrumentation must be added before adding Eventuous services
builder.Services.AddTelemetry();
builder.Services.AddEventuous(builder.Configuration);

var app = builder.Build();

app.Services.AddEventuousLogs();
app.UseSwagger(c=>c.RouteTemplate = "openapi/{documentName}.json");
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
