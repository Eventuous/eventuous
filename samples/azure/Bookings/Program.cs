using Bookings;
using Bookings.Domain.Bookings;
using Bookings.Infrastructure;
using Eventuous;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
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

app.UseSerilogRequestLogging();
app.UseEventuousLogs();
app.UseSwagger().UseSwaggerUI();
app.MapControllers();
app.UseOpenTelemetryPrometheusScrapingEndpoint();

try {
    app.Run("http://*:5051");
    return 0;
}
catch (Exception e) {
    Log.Fatal(e, "Host terminated unexpectedly");
    return 1;
}
finally {
    Log.CloseAndFlush();
}