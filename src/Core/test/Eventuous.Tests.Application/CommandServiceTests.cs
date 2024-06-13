using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Testing;
using NodaTime;

namespace Eventuous.Tests.Application;

public class CommandServiceTests {
    readonly AggregateStore _aggregateStore;

    static CommandServiceTests() {
        TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.RoomBooked).Assembly);
    }

    public CommandServiceTests() {
        var store = new InMemoryEventStore();
        _aggregateStore = new AggregateStore(store);
    }

    [Fact]
    public async Task HandleFirstCommandThreadSafe() {
        const int threadCount = 3;

        var service = new BookingService(_aggregateStore);
        var tasks = Enumerable
            .Range(1, threadCount)
            .Select(bookingId => Task.Run(() => service.Handle(GetBookRoom(bookingId.ToString()), default)))
            .ToList();

        await Task.WhenAll(tasks);

        foreach (var task in tasks) {
            var result = await task;

            result.Success.Should().BeTrue();
            result.Changes.Should().HaveCount(1);
        }
    }

    static Commands.BookRoom GetBookRoom(string bookingId = "123") {
        var checkIn  = LocalDate.FromDateTime(DateTime.Today);
        var checkOut = checkIn.PlusDays(1);

        return new Commands.BookRoom(bookingId, "234", checkIn, checkOut, 100);
    }
}