using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers.TUnit;

namespace Eventuous.Tests.EventStore;

[ClassDataSource<StoreFixture>(Shared = SharedType.None)]
public class AppServiceTests {
    readonly TestEventListener _listener = new();
    readonly StoreFixture      _fixture;

    public AppServiceTests(StoreFixture fixture) {
        _fixture = fixture;
        _fixture.TypeMapper.AddType<BookingEvents.BookingImported>();
    }

    BookingService Service { get; set; } = null!;

    [Before(Test)]
    public void BeforeTest() {
        Service = new(_fixture.EventStore);
    }

    [Test]
    [Category("Application")]
    public async Task ProcessAnyForNew(CancellationToken cancellationToken) {
        var cmd = DomainFixture.CreateImportBooking();

        var expected = new object[] { new BookingEvents.BookingImported(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut) };

        var handlingResult = await Service.Handle(cmd, cancellationToken);
        handlingResult.Success.Should().BeTrue();

        var events = await _fixture.EventStore.ReadEvents(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start, int.MaxValue, cancellationToken);

        var result = events.Select(x => x.Payload).ToArray();

        result.Should().BeEquivalentTo(expected);
    }

    [After(Test)]
    public void Dispose() => _listener.Dispose();
}
