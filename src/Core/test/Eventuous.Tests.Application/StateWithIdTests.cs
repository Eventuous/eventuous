using NodaTime;

namespace Eventuous.Tests.Application;

using Sut.App;
using Sut.Domain;
using Testing;

public class StateWithIdTests {
    readonly BookingService _service;
    readonly AggregateStore _aggregateStore;

    public StateWithIdTests() {
        _aggregateStore = new(new InMemoryEventStore());
        _service        = new(_aggregateStore);
    }

    [Fact]
    public async Task ShouldGetIdForNew() {
        var map   = new StreamNameMap();
        var id    = Guid.NewGuid().ToString();
        var state = await Seed(id);

        var bookingId = new BookingId(id);

        // Ensure that the id was set when the aggregate was created
        state.State!.Id.Should().Be(bookingId);

        var instance = await _aggregateStore.Load<Booking, BookingState, BookingId>(map, bookingId, default);

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
