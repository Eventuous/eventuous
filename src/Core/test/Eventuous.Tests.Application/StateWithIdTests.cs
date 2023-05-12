using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Eventuous.Tests.Application;

public class StateWithIdTests {
    readonly BookingService _service;
    readonly AggregateStore _aggregateStore;

    public StateWithIdTests() {
        var store = new InMemoryEventStore();
        _aggregateStore = new AggregateStore(store, memoryCache: new MemoryCache(Options.Create<MemoryCacheOptions>(new())));
        _service        = new BookingService(_aggregateStore);
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
