using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using Eventuous.TestHelpers.Fakes;
using NodaTime;

namespace Eventuous.Tests.Application;

public class FunctionalServiceTests : IDisposable {
    readonly InMemoryEventStore _store;
    readonly BookingFuncService _service;
    readonly TestEventListener  _listener;

    static FunctionalServiceTests() {
        TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.RoomBooked).Assembly);
    }

    public FunctionalServiceTests(ITestOutputHelper output) {
        _store    = new InMemoryEventStore();
        _service  = new BookingFuncService(_store);
        _listener = new TestEventListener(output);
    }

    [Fact]
    public async Task ExecuteOnNewStream() {
        var cmd = await Seed();

        var stream = await _store.ReadEvents(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start, 100, CancellationToken.None);

        var expected = new BookingEvents.RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price);

        stream.Should().HaveCount(1);
        var actual = stream[0].Payload;
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ExecuteOnExistingStream() {
        var bookRoom    = await Seed();
        var paymentTime = DateTimeOffset.Now;
        var cmd         = new Commands.RecordPayment(new BookingId(bookRoom.BookingId), "444", new Money(bookRoom.Price), paymentTime);

        var result = await _service.Handle(cmd, default);

        var expectedResult = new object[] {
            new BookingEvents.BookingPaymentRegistered(cmd.PaymentId, cmd.Amount.Amount),
            new BookingEvents.BookingFullyPaid(paymentTime)
        };

        result.Changes.Should().HaveCount(2);
        var newEvents = result.Changes!.Select(x => x.Event);
        newEvents.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task ExecuteOnAnyForNewStream() {
        var bookRoom = GetBookRoom();

        var cmd = new Commands.ImportBooking {
            BookingId = "dummy",
            Price     = bookRoom.Price,
            CheckIn   = bookRoom.CheckIn,
            CheckOut  = bookRoom.CheckOut,
            RoomId    = bookRoom.RoomId
        };
        var result = await _service.Handle(cmd, default);
        result.Success.Should().BeTrue();
        result.Changes.Should().HaveCount(1);
    }

    [Fact]
    public async Task AmendEventAddsMeta() {
        var service = new BookingFuncService(_store, amendEvent: AddMeta);
        var cmd     = GetBookRoom();

        await service.Handle(cmd, default);
        
        var stream = await _store.ReadStream(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start, true, default);
        stream[0].Metadata["foo"].Should().Be("bar");

        return;

        StreamEvent AddMeta(StreamEvent evt) => evt with { Metadata = new Metadata { ["foo"] = "bar" } };
    }

    static Commands.BookRoom GetBookRoom() {
        var checkIn  = LocalDate.FromDateTime(DateTime.Today);
        var checkOut = checkIn.PlusDays(1);

        return new Commands.BookRoom("123", "234", checkIn, checkOut, 100);
    }

    async Task<Commands.BookRoom> Seed() {
        var cmd = GetBookRoom();
        await _service.Handle(cmd, default);

        return cmd;
    }

    public void Dispose()
        => _listener.Dispose();
}
