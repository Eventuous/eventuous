using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers.TUnit;
using Shouldly;

namespace Eventuous.Tests.KurrentDB;

[ClassDataSource<StoreFixture>(Shared = SharedType.None)]
public class AppServiceTests {
    readonly TestEventListener _listener = new();
    readonly StoreFixture      _fixture;

    public AppServiceTests(StoreFixture fixture) {
        _fixture = fixture;
        _fixture.TypeMapper.RegisterDiscoveredTypes();
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
        handlingResult.Success.ShouldBeTrue();

        var events = await _fixture.EventStore.ReadEvents(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start, int.MaxValue, true, cancellationToken);

        var result = events.Select(x => x.Payload).ToArray();

        result.ShouldBeEquivalentTo(expected);
    }

    [Test]
    public async Task ProcessNewThenDeleteAndDoItAgain(CancellationToken cancellationToken) {
        // This will create a new stream
        var cmd = DomainFixture.CreateImportBooking();
        await Service.Handle(cmd, cancellationToken);

        var streamName = StreamName.For<Booking>(cmd.BookingId);
        await _fixture.EventStore.DeleteStream(streamName, ExpectedStreamVersion.Any, cancellationToken);

        var handlingResult = await Service.Handle(cmd, cancellationToken);
        handlingResult.Success.ShouldBeTrue();

        var cancelCmd    = new Commands.CancelBooking(new(cmd.BookingId));
        var secondResult = await Service.Handle(cancelCmd, cancellationToken);
        secondResult.Success.ShouldBeTrue();
    }

    [After(Test)]
    public void Dispose() => _listener.Dispose();
}
