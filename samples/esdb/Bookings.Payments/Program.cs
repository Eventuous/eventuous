using Bookings.Payments;
using Bookings.Payments.Application;
using Bookings.Payments.Domain;
using Bookings.Payments.Infrastructure;
using Eventuous;
using Serilog;

TypeMap.RegisterKnownEventTypes();
Logging.ConfigureLog();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OpenTelemetry instrumentation must be added before adding Eventuous services
builder.Services.AddTelemetry();

builder.Services.AddServices(builder.Configuration);
builder.Host.UseSerilog();

var app = builder.Build();

app.UseSwagger();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.UseEventuousLogs();

app.MapCommandFunc<PaymentCommands.RecordPayment, Payment, Result<Payment>>();

app.UseSwaggerUI();

app.Run();