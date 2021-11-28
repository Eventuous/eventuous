using Eventuous.AspNetCore.Web;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using static Eventuous.Sut.Domain.BookingEvents;
using static Eventuous.Tests.Projections.MongoDB.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Projections.MongoDB;

public sealed class ProjectingWithTypedHandlers : IDisposable {
    readonly TestServer _host;

    public ProjectingWithTypedHandlers(ITestOutputHelper output) {
        var builder = new WebHostBuilder()
            .ConfigureLogging(cfg => cfg.AddXunit(output, LogLevel.Debug).SetMinimumLevel(LogLevel.Debug))
            .ConfigureServices(ConfigureServices)
            .Configure(x => x.UseEventuousLogs());

        _host = new TestServer(builder);
    }

    void ConfigureServices(IServiceCollection services)
        => services
            .AddSingleton(Instance.Client)
            .AddSingleton(Instance.Mongo)
            .AddCheckpointStore<MongoCheckpointStore>()
            .AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
                "testSub",
                builder => builder.AddEventHandler<SutProjection>()
            );

    [Fact]
    public async Task ShouldProjectImported() {
        var evt    = DomainFixture.CreateImportBooking();
        var stream = StreamName.For<Booking>(evt.BookingId);

        var append = await Instance.EventStore.AppendEvents(
            stream,
            ExpectedStreamVersion.Any,
            new[] {
                new StreamEvent(evt, new Metadata(), "application/json", 0)
            },
            CancellationToken.None
        );

        await Task.Delay(500);

        var expected = new BookingDocument(evt.BookingId) {
            RoomId       = evt.RoomId,
            CheckInDate  = evt.CheckIn,
            CheckOutDate = evt.CheckOut,
            Position     = append.GlobalPosition
        };

        var actual = await Instance.Mongo.LoadDocument<BookingDocument>(evt.BookingId);
        actual.Should().Be(expected);
    }

    public void Dispose() => _host.Dispose();
}

class SutProjection : MongoProjection<BookingDocument> {
    public SutProjection(IMongoDatabase database) : base(database) {
        On<BookingImported>(
            evt => evt.BookingId,
            (evt, update) => update
                .SetOnInsert(x => x.RoomId, evt.RoomId)
                .Set(x => x.CheckInDate, evt.CheckIn)
                .Set(x => x.CheckOutDate, evt.CheckOut)
        );
    }
}