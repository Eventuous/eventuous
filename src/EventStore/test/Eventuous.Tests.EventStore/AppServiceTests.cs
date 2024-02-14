using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;

namespace Eventuous.Tests.EventStore;

public class AppServiceTests(StoreFixture fixture, ITestOutputHelper output) : IClassFixture<StoreFixture>, IDisposable {
    readonly TestEventListener _listener = new(output);

    BookingService Service { get; } = new(fixture.AggregateStore);

    [Fact]
    public async Task ProcessAnyForNew() {
        var cmd = DomainFixture.CreateImportBooking();

        var expected = new object[] { new BookingEvents.BookingImported(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut) };

        var handlingResult = await Service.Handle(cmd, default);
        handlingResult.Success.Should().BeTrue();

        var events = await fixture.EventStore.ReadEvents(
            StreamName.For<Booking>(cmd.BookingId),
            StreamReadPosition.Start,
            int.MaxValue,
            default
        );

        var result = events.Select(x => x.Payload).ToArray();

        result.Should().BeEquivalentTo(expected);
    }

    public void Dispose() => _listener.Dispose();
}
