using Bookings.Payments;
using Bookings.Payments.Domain;
using Bookings.Payments.Infrastructure;
using Eventuous.Spyglass;
using Serilog;

Logging.ConfigureLog();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// OpenTelemetry instrumentation must be added before adding Eventuous services
builder.Services.AddTelemetry();

builder.Services.AddServices(builder.Configuration);
builder.Host.UseSerilog();

var app = builder.Build();
app.Services.AddEventuousLogs();

// Serve the OpenAPI document where the Scalar API reference in the Aspire AppHost expects it
app.UseSwagger(c => c.RouteTemplate = "openapi/{documentName}.json");
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Here we discover commands by their annotations
app.MapDiscoveredCommands<PaymentState>();

app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "Payments v1"));

app.MapEventuousSpyglass();
app.MapHealthChecks("/health");

app.Run();