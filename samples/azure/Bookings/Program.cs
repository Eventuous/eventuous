using Bookings;
using Bookings.Application;
using Bookings.Domain.Bookings;
using Bookings.Infrastructure;
using Eventuous;
using Eventuous.Spyglass;
using Microsoft.OpenApi.Models;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Serilog;
using static Bookings.Integration.IntegrationEvents;

TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.V1.RoomBooked).Assembly);
TypeMap.RegisterKnownEventTypes(typeof(BookingPaymentRecorded).Assembly);
Logging.ConfigureLog();

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Trace).AddConsole();
builder.Host.UseSerilog();

builder.Services
    .AddControllers()
    .AddJsonOptions(cfg => cfg.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "Bookings API", Version = "v1" });
    c.AddServer(new OpenApiServer { Url = "/" }); // Relative path
});
builder.Services.AddTelemetry();
builder.Services.AddEventuous(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseEventuousLogs();
app.UseSwagger(c=>c.RouteTemplate = "openapi/{documentName}.json");
app.MapControllers();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapEventuousSpyglass();

app.MapHealthChecks("/health");

app.MapGet(
    "/bookings/my/{userId}",
    async (string userId, BookingsQueryService queryService) => {
        var userBookings = await queryService.GetUserBookings(userId);

        return userBookings == null ? Results.NotFound() : Results.Ok(userBookings);
    }
).WithTags("QueryApi");

app.Run();