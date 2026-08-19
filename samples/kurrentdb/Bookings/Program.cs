using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Bookings;
using Bookings.Application;
using Bookings.Application.Queries;
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

EventSerializer.SetDefault(new DefaultStaticEventSerializer(new SourceGenerationContext()));
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddControllers().AddJsonOptions(cfg => cfg.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddTelemetry();
builder.Services.AddEventuous(builder.Configuration);
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));

var app = builder.Build();

app.UseSerilogRequestLogging();
// Serve the OpenAPI document where the Scalar API reference in the Aspire AppHost expects it
app.UseSwagger(c => c.RouteTemplate = "openapi/{documentName}.json");
app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "Bookings v1"));
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
);

// Unlike GET /bookings/{id}, which folds the state from the event stream, this endpoint
// serves the read model projected to Azure Blob Storage
app.MapGet(
    "/bookings/{bookingId}/view",
    async (string bookingId, BookingsQueryService queryService, CancellationToken cancellationToken) => {
        var booking = await queryService.GetBooking(bookingId, cancellationToken);

        return booking == null ? Results.NotFound() : Results.Ok(booking);
    }
);

// The blob projector doesn't create the container, and outside Aspire nothing else does
app.Services.GetRequiredService<BlobServiceClient>()
    .GetBlobContainerClient(BookingStateBlobProjection.ContainerName)
    .CreateIfNotExists();

var factory  = app.Services.GetRequiredService<ILoggerFactory>();
var listener = new LoggingEventListener(factory, "OpenTelemetry");

// The Aspire AppHost assigns URLs via ASPNETCORE_URLS; keep the fixed port for standalone runs
if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") == null) app.Urls.Add("http://*:5051");

try {
    app.Run();

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
