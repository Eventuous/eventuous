using NodaTime;

namespace Eventuous.Tests.Application;

using Sut.App;
using Sut.Domain;
using Testing;

public class StateWithIdTests {
    readonly BookingService _service;
    readonly IEventStore    _store = new InMemoryEventStore();

    public StateWithIdTests() => _service = new(_store);

    [Test]
    public async Task ShouldGetIdForNew(CancellationToken cancellationToken) {
        var map   = new StreamNameMap();
        var id    = Guid.NewGuid().ToString();
        var result = await Seed(id);

        var bookingId = new BookingId(id);

        // Ensure that the id was set when the aggregate was created
        result.TryGet(out var ok).Should().BeTrue();
        ok!.State.Id.Should().Be(bookingId);

        var instance = await _store.LoadAggregate<Booking, BookingState, BookingId>(bookingId, map, true, cancellationToken: cancellationToken);

        // Ensure that the id was set when the aggregate was loaded
        instance.State.Id.Should().Be(bookingId);
    }

    async Task<Result<BookingState>> Seed(string id) {
        var checkIn  = LocalDate.FromDateTime(DateTime.Today);
        var checkOut = checkIn.PlusDays(1);
        var cmd      = new Commands.BookRoom(id, "234", checkIn, checkOut, 100);

        return await _service.Handle(cmd, default);
    }
}
