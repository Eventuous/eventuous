using System.Text.Json.Serialization;
using Bookings;
using Bookings.Application;
using Bookings.Domain.Bookings;
using Eventuous;
using Eventuous.Diagnostics.Logging;
using Eventuous.Spyglass;
using Microsoft.AspNetCore.Http.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Grpc", LogEventLevel.Information)
    .MinimumLevel.Override("Grpc.Net.Client.Internal.GrpcCall", LogEventLevel.Error)
    .MinimumLevel.Override("Microsoft.AspNetCore.Mvc.Infrastructure", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    // .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

DefaultEventSerializer.SetDefaultSerializer(new DefaultStaticEventSerializer(new SourceGenerationContext()));
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddControllers().AddJsonOptions(cfg => cfg.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTelemetry();
builder.Services.AddEventuous(builder.Configuration);
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger().UseSwaggerUI();
app.MapControllers();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapEventuousSpyglass();

app.MapGet(
    "/bookings/my/{userId}",
    async (string userId, BookingsQueryService queryService) => {
        var userBookings = await queryService.GetUserBookings(userId);

        return userBookings == null ? Results.NotFound() : Results.Ok(userBookings);
    }
);

var factory  = app.Services.GetRequiredService<ILoggerFactory>();
var listener = new LoggingEventListener(factory, "OpenTelemetry");

try {
    app.Run("http://*:5051");

    return 0;
} catch (Exception e) {
    Log.Fatal(e, "Host terminated unexpectedly");

    return 1;
} finally {
    Log.CloseAndFlush();
    listener.Dispose();
}

[JsonSerializable(typeof(BookingEvents.V1.RoomBooked))]
[JsonSerializable(typeof(BookingEvents.V1.BookingCancelled))]
[JsonSerializable(typeof(BookingEvents.V1.BookingFullyPaid))]
[JsonSerializable(typeof(BookingEvents.V1.BookingOverpaid))]
[JsonSerializable(typeof(BookingEvents.V1.PaymentRecorded))]
internal partial class SourceGenerationContext : JsonSerializerContext;
