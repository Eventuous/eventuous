global using NodaTime;
using Eventuous.Tests.Fixtures;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests;

public class StoringEventsWithCustomStream : NaiveFixture {
    public StoringEventsWithCustomStream() {
        var streamNameMap = new StreamNameMap();
        streamNameMap.Register<BookingId>(GetStreamName);
        Service = new BookingService(AggregateStore, streamNameMap);
        TypeMap.RegisterKnownEventTypes();
    }

    BookingService Service { get; }

    [Fact]
    public async Task TestOnNew() {
        var cmd = CreateBookRoomCommand();

        Change[] expected = [ new Change(new RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price), "RoomBooked") ];

        var result = await Service.Handle(cmd, default);

        result.Success.Should().BeTrue();
        result.Changes.Should().BeEquivalentTo(expected);

        var evt = await EventStore.ReadEvents(
            GetStreamName(new BookingId(cmd.BookingId)),
            StreamReadPosition.Start,
            1,
            CancellationToken.None
        );

        evt[0].Payload.Should().BeEquivalentTo(result.Changes!.First().Event);
    }

    [Fact]
    public async Task TestOnExisting() {
        var cmd = CreateBookRoomCommand();

        await Service.Handle(cmd, default);

        var secondCmd = new Commands.RecordPayment(
            new BookingId(cmd.BookingId),
            Auto.Create<string>(),
            new Money(cmd.Price),
            DateTimeOffset.Now
        );

        var expected = new Change[] {
            new(
                new BookingPaymentRegistered(
                    secondCmd.PaymentId,
                    secondCmd.Amount.Amount
                ),
                "PaymentRegistered"
            ),
            new(new BookingOutstandingAmountChanged(0), "OutstandingAmountChanged"),
            new(new BookingFullyPaid(secondCmd.PaidAt), "BookingFullyPaid")
        };

        var result = await Service.Handle(secondCmd, default);

        result.Success.Should().BeTrue();
        result.Changes.Should().BeEquivalentTo(expected);

        var evt = await EventStore.ReadEvents(
            GetStreamName(new BookingId(cmd.BookingId)),
            StreamReadPosition.Start,
            100,
            CancellationToken.None
        );

        var actual = evt.Skip(1).Select(x => x.Payload);

        actual.Should().BeEquivalentTo(expected.Select(x => x.Event));
    }

    static StreamName GetStreamName(BookingId bookingId)
        => new($"hotel-booking-{bookingId}");
}
