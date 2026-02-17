extern alias BookingsApp;
extern alias PaymentsApp;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Eventuous.Spyglass;
using Eventuous.Testing;
using JetBrains.Annotations;
using Microsoft.AspNetCore.TestHost;
using static Bookings.Domain.Bookings.BookingEvents.V1;

namespace Eventuous.Tests.Spyglass;

public class SpyglassApiTests {
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    static async Task<(WebApplication App, HttpClient Client)> CreateTestApp() {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<IEventStore>(new InMemoryEventStore());
        builder.Environment.EnvironmentName = "Development";
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.MapEventuousSpyglass();
        await app.StartAsync();

        var client = app.GetTestClient();

        return (app, client);
    }

    [Test]
    public async Task Ping_returns_ok() {
        var (app, client) = await CreateTestApp();

        try {
            using var response = await client.GetAsync("/spyglass/ping");

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        } finally {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    [Test]
    public async Task Aggregates_contains_booking() {
        // Force-load the Bookings assembly so its module initializer populates SpyglassRegistry
        RuntimeHelpers.RunModuleConstructor(typeof(BookingsApp::Bookings.Registrations).Module.ModuleHandle);

        var (app, client) = await CreateTestApp();

        try {
            using var response = await client.GetAsync("/spyglass/aggregates");

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

            var json       = await response.Content.ReadAsStringAsync();
            var aggregates = JsonSerializer.Deserialize<AggregateEntry[]>(json, JsonOptions)!;
            var booking    = aggregates.FirstOrDefault(a => a.StateType == "BookingState");

            await Assert.That(booking).IsNotNull();
            await Assert.That(booking!.Id).IsNotEqualTo(Guid.Empty);
            await Assert.That(booking.AggregateType).IsEqualTo("Booking");
            await Assert.That(booking.Methods).Contains("BookRoom");
            await Assert.That(booking.Methods).Contains("RecordPayment");
            await Assert.That(booking.Events).Contains("RoomBooked");
            await Assert.That(booking.Events).Contains("PaymentRecorded");
            await Assert.That(booking.Events).Contains("BookingFullyPaid");
        } finally {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    [Test]
    public async Task Aggregates_contains_payment_state_as_standalone() {
        // Force-load the Bookings.Payments assembly so its module initializer populates SpyglassRegistry
        RuntimeHelpers.RunModuleConstructor(
            typeof(PaymentsApp::Bookings.Payments.Registrations).Module.ModuleHandle
        );

        var (app, client) = await CreateTestApp();

        try {
            using var response = await client.GetAsync("/spyglass/aggregates");

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

            var json       = await response.Content.ReadAsStringAsync();
            var aggregates = JsonSerializer.Deserialize<AggregateEntry[]>(json, JsonOptions)!;
            var payment    = aggregates.FirstOrDefault(a => a.StateType == "PaymentState");

            await Assert.That(payment).IsNotNull();
            await Assert.That(payment!.Id).IsNotEqualTo(Guid.Empty);
            await Assert.That(payment.AggregateType).IsNull();
            await Assert.That(payment.Methods).IsEmpty();
            await Assert.That(payment.Events).Contains("PaymentRecorded");
        } finally {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    [Test]
    public async Task Load_returns_booking_state_and_events() {
        RuntimeHelpers.RunModuleConstructor(typeof(BookingsApp::Bookings.Registrations).Module.ModuleHandle);

        var (app, client) = await CreateTestApp();

        try {
            var eventStore = app.Services.GetRequiredService<IEventStore>();

            // Get the Booking type's registry id
            using var aggResponse = await client.GetAsync("/spyglass/aggregates");
            var aggJson    = await aggResponse.Content.ReadAsStringAsync();
            var aggregates = JsonSerializer.Deserialize<AggregateEntry[]>(aggJson, JsonOptions)!;
            var booking    = aggregates.First(a => a.AggregateType == "Booking");

            // Write events to the in-memory store
            var entityId   = Guid.NewGuid().ToString();
            var streamName = new StreamName($"Booking-{entityId}");

            await eventStore.AppendEvents(
                streamName,
                ExpectedStreamVersion.NoStream,
                [
                    new(Guid.NewGuid(), new RoomBooked("guest-1", "room-42", new(2025, 1, 1), new(2025, 1, 3), 200f, 50f, 150f, "USD", DateTimeOffset.UtcNow), new()),
                    new(Guid.NewGuid(), new PaymentRecorded(150f, 0f, "USD", "pay-1", "guest-1", DateTimeOffset.UtcNow), new())
                ],
                default
            );

            // Call the load endpoint
            using var loadResponse = await client.GetAsync($"/spyglass/load/{booking.Id}/{entityId}?version=-1");

            await Assert.That(loadResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

            var loadJson = await loadResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(loadJson);
            var root = doc.RootElement;

            // State should reflect the loaded events
            var state = root.GetProperty("state");
            await Assert.That(state.GetProperty("guestId").GetString()).IsEqualTo("guest-1");
            await Assert.That(state.GetProperty("paid").GetBoolean()).IsEqualTo(false);

            // Events should contain both events
            var events = root.GetProperty("events");
            await Assert.That(events.GetArrayLength()).IsEqualTo(2);
            await Assert.That(events[0].GetProperty("eventType").GetString()).IsEqualTo("RoomBooked");
            await Assert.That(events[1].GetProperty("eventType").GetString()).IsEqualTo("PaymentRecorded");
        } finally {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    [Test]
    public async Task Load_returns_not_found_for_nonexistent_stream() {
        RuntimeHelpers.RunModuleConstructor(typeof(BookingsApp::Bookings.Registrations).Module.ModuleHandle);

        var (app, client) = await CreateTestApp();

        try {
            // Get the Booking type's registry id
            using var aggResponse = await client.GetAsync("/spyglass/aggregates");
            var aggJson    = await aggResponse.Content.ReadAsStringAsync();
            var aggregates = JsonSerializer.Deserialize<AggregateEntry[]>(aggJson, JsonOptions)!;
            var booking    = aggregates.First(a => a.AggregateType == "Booking");

            // Load a non-existing entity
            using var loadResponse = await client.GetAsync($"/spyglass/load/{booking.Id}/does-not-exist?version=-1");

            await Assert.That(loadResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        } finally {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    [UsedImplicitly]
    record AggregateEntry(Guid Id, string? AggregateType, string StateType, string[] Methods, string[] Events);
}
