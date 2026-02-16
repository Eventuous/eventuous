extern alias BookingsApp;
extern alias PaymentsApp;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Eventuous.Spyglass;
using Eventuous.Testing;
using JetBrains.Annotations;
using Microsoft.AspNetCore.TestHost;

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

            var content = await response.Content.ReadAsStringAsync();
            await Assert.That(content).Contains("Okay");
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
            await Assert.That(booking!.Type).IsEqualTo("Booking");
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
            await Assert.That(payment!.Type).IsNull();
            await Assert.That(payment.Methods).IsEmpty();
            await Assert.That(payment.Events).Contains("PaymentRecorded");
        } finally {
            client.Dispose();
            await app.DisposeAsync();
        }
    }

    [UsedImplicitly]
    record AggregateEntry(string? Type, string StateType, string[] Methods, string[] Events);
}
