using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.EventStore;

public class AppServiceTests : IDisposable {
    readonly TestEventListener _listener;

    static BookingService Service { get; } = new(Instance.AggregateStore);

    public AppServiceTests(ITestOutputHelper output) => _listener = new TestEventListener(output);

    [Fact]
    public async Task ProcessAnyForNew() {
        var cmd = DomainFixture.CreateImportBooking();

        var expected = new object[] {
            new BookingEvents.BookingImported(
                cmd.RoomId,
                cmd.Price,
                cmd.CheckIn,
                cmd.CheckOut
            )
        };

        var handlingResult = await Service.Handle(cmd, default);
        handlingResult.Success.Should().BeTrue();

        var events = await Instance.EventStore.ReadEvents(
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