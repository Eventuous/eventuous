using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using Eventuous.Testing;
using NodaTime;
using Shouldly;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Application;

public abstract class ServiceTestBase : IDisposable {
    [Fact]
    public async Task ExecuteOnNewStream() {
        var cmd      = Helpers.GetBookRoom();
        var expected = new RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price);

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(cmd.BookingId)
            .When(cmd)
            .Then(result => result.ResultIsOk(x => x.Changes.Should().HaveCount(1)).FullStreamEventsAre(expected));
    }

    [Fact]
    public async Task ExecuteOnExistingStream() {
        var seedCmd = Helpers.GetBookRoom();
        var seed    = new RoomBooked(seedCmd.RoomId, seedCmd.CheckIn, seedCmd.CheckOut, seedCmd.Price);

        var paymentTime = DateTimeOffset.Now;
        var cmd         = new Commands.RecordPayment(new(seedCmd.BookingId), "444", new(seedCmd.Price), paymentTime);

        var expectedResult = new object[] {
            new BookingPaymentRegistered(cmd.PaymentId, cmd.Amount.Amount),
            new BookingOutstandingAmountChanged(0),
            new BookingFullyPaid(paymentTime)
        };

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(seedCmd.BookingId, seed)
            .When(cmd)
            .Then(result => result.ResultIsOk().NewStreamEventsAre(expectedResult));
    }

    [Fact]
    public async Task ExecuteOnAnyForNewStream() {
        var bookRoom = Helpers.GetBookRoom();

        var cmd = new Commands.ImportBooking {
            BookingId = "dummy",
            Price     = bookRoom.Price,
            CheckIn   = bookRoom.CheckIn,
            CheckOut  = bookRoom.CheckOut,
            RoomId    = bookRoom.RoomId
        };

        await CommandServiceFixture
            .ForService(() => CreateService(), Store)
            .Given(cmd.BookingId)
            .When(cmd)
            .Then(result => result.ResultIsOk(x => x.Changes.Should().HaveCount(1)).StreamIs(x => x.Length.ShouldBe(1)));
    }

    [Fact]
    public async Task Ensure_builder_is_thread_safe() {
        const int threadCount = 3;

        var service = CreateService();

        var tasks = Enumerable
            .Range(1, threadCount)
            .Select(bookingId => Task.Run(() => service.Handle(Helpers.GetBookRoom(bookingId.ToString()), default)))
            .ToList();

        await Task.WhenAll(tasks);

        foreach (var task in tasks) {
            var result = await task;

            result.TryGet(out var ok).Should().BeTrue();
            ok!.Changes.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task Should_amend_event_from_command() {
        var service = CreateService(amendEvent: AmendEvent);
        var cmd     = CreateCommand();

        await service.Handle(cmd, default);

        var stream = await Store.ReadStream(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start);
        stream[0].Metadata["userId"].Should().Be(cmd.ImportedBy);
    }

    [Fact]
    public async Task Should_amend_event_with_static_meta() {
        var cmd = Helpers.GetBookRoom();

        await CommandServiceFixture
            .ForService(() => CreateService(amendAll: AddMeta), Store)
            .Given(cmd.BookingId)
            .When(cmd)
            .Then(x => x.StreamIs(e => e[0].Metadata["foo"].Should().Be("bar")));
    }

    [Fact]
    public async Task Should_combine_amendments() {
        var service = CreateService(amendEvent: AmendEvent, amendAll: AddMeta);
        var cmd     = CreateCommand();

        await service.Handle(cmd, default);

        var stream = await Store.ReadStream(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start);
        stream[0].Metadata["userId"].Should().Be(cmd.ImportedBy);
        stream[0].Metadata["foo"].Should().Be("bar");
    }

    static NewStreamEvent AmendEvent(NewStreamEvent evt, ImportBooking cmd) => evt with { Metadata = evt.Metadata.With("userId", cmd.ImportedBy) };

    static NewStreamEvent AddMeta(NewStreamEvent evt) => evt with { Metadata = evt.Metadata.With("foo", "bar") };

    static ImportBooking CreateCommand() {
        var today = LocalDate.FromDateTime(DateTime.Today);

        return new("booking1", "room1", 100, today, today.PlusDays(1), "user");
    }

    static async Task<Commands.BookRoom> Seed(ICommandService<BookingState> service) {
        var cmd = Helpers.GetBookRoom();
        await service.Handle(cmd, default);

        return cmd;
    }

    protected ServiceTestBase(ITestOutputHelper output) {
        _listener = new(output);
        TypeMap.RegisterKnownEventTypes(typeof(RoomBooked).Assembly);
    }

    protected readonly TypeMapper         TypeMap = new();
    protected readonly InMemoryEventStore Store   = new();

    readonly TestEventListener _listener;

    protected abstract ICommandService<BookingState> CreateService(
            AmendEvent<ImportBooking>? amendEvent = null,
            AmendEvent?                amendAll   = null
        );

    protected record ImportBooking(
            string    BookingId,
            string    RoomId,
            float     Price,
            LocalDate CheckIn,
            LocalDate CheckOut,
            string    ImportedBy
        );

    public void Dispose() => _listener.Dispose();
}
