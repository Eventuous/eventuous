using Bookings.Infrastructure;
using Bookings.Payments;
using Bookings.Payments.Domain;
using Eventuous;
using Microsoft.OpenApi.Models;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Serilog;
using static Bookings.Payments.Application.PaymentCommands;

TypeMap.RegisterKnownEventTypes();
Logging.ConfigureLog();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddControllers()
    .AddJsonOptions(cfg => cfg.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
builder.Services.ConfigureHttpJsonOptions(cfg => cfg.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "Bookings API", Version = "v1" });
    c.AddServer(new OpenApiServer { Url = "/" }); // Relative path
});
// OpenTelemetry instrumentation must be added before adding Eventuous services
builder.Services.AddTelemetry();
builder.Services.AddEventuous(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.Services.AddEventuousLogs();
app.UseSwagger(c=>c.RouteTemplate = "openapi/{documentName}.json");
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapHealthChecks("/health");

// Here we discover commands by their annotations
app.MapCommands<PaymentState>().MapCommand<RecordPayment>();

app.Run();