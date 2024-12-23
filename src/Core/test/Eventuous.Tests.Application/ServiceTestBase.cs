using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers.TUnit;
using Eventuous.Testing;
using NodaTime;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Application;

public abstract partial class ServiceTestBase {
    [Test]
    public async Task Ensure_builder_is_thread_safe(CancellationToken cancellationToken) {
        const int threadCount = 3;

        var service = CreateService();

        var tasks = Enumerable
            .Range(1, threadCount)
            .Select(bookingId => Task.Run(() => service.Handle(Helpers.GetBookRoom(bookingId.ToString()), cancellationToken), cancellationToken))
            .ToList();

        await Task.WhenAll(tasks);

        foreach (var task in tasks) {
            var result = await task;

            result.TryGet(out var ok).Should().BeTrue();
            ok!.Changes.Should().HaveCount(1);
        }
    }

    static ImportBooking CreateCommand() {
        var today = LocalDate.FromDateTime(DateTime.Today);

        return new("booking1", "room1", 100, today, today.PlusDays(1), "user");
    }

    static async Task<Commands.BookRoom> Seed(ICommandService<BookingState> service) {
        var cmd = Helpers.GetBookRoom();
        await service.Handle(cmd, default);

        return cmd;
    }

    protected ServiceTestBase() {
        _listener = new();
        TypeMap.RegisterKnownEventTypes(typeof(RoomBooked).Assembly);
    }

    protected readonly TypeMapper         TypeMap = new();
    protected readonly InMemoryEventStore Store   = new();

    readonly TestEventListener _listener;

    protected abstract ICommandService<BookingState> CreateService(AmendEvent<ImportBooking>? amendEvent = null, AmendEvent? amendAll = null);

    protected record ImportBooking(
            string    BookingId,
            string    RoomId,
            float     Price,
            LocalDate CheckIn,
            LocalDate CheckOut,
            string    ImportedBy
        );

    [After(Test)]
    public void Dispose() => _listener.Dispose();
}
