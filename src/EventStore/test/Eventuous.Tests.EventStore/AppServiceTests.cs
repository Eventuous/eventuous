using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;

namespace Eventuous.Tests.EventStore;

public class AppServiceTests : IClassFixture<StoreFixture>, IDisposable {
    readonly TestEventListener _listener;
    readonly StoreFixture      _fixture;

    public AppServiceTests(StoreFixture fixture, ITestOutputHelper output) {
        _fixture  = fixture;
        _listener = new(output);
        Service   = new(fixture.EventStore);
        _fixture.TypeMapper.AddType<BookingEvents.BookingImported>();
    }

    BookingService Service { get; }

    [Fact]
    [Trait("Category", "Application")]
    public async Task ProcessAnyForNew() {
        var cmd = DomainFixture.CreateImportBooking();

        var expected = new object[] { new BookingEvents.BookingImported(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut) };

        var handlingResult = await Service.Handle(cmd, default);
        handlingResult.Success.Should().BeTrue();

        var events = await _fixture.EventStore.ReadEvents(
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
