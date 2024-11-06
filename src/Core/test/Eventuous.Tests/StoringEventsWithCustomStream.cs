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
        Service = new(EventStore, streamNameMap);
        TypeMap.RegisterKnownEventTypes();
    }

    BookingService Service { get; }

    [Test]
    public async Task TestOnNew(CancellationToken cancellationToken) {
        var cmd = CreateBookRoomCommand();

        Change[] expected = [new(new RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price), TypeNames.RoomBooked)];

        var result = await Service.Handle(cmd, cancellationToken);

        result.TryGet(out var ok).Should().BeTrue();
        ok!.Changes.Should().BeEquivalentTo(expected);

        var evt = await EventStore.ReadEvents(GetStreamName(new(cmd.BookingId)), StreamReadPosition.Start, 1, CancellationToken.None);

        evt[0].Payload.Should().BeEquivalentTo(ok.Changes.First().Event);
    }

    [Test]
    public async Task TestOnExisting(CancellationToken cancellationToken) {
        var cmd = CreateBookRoomCommand();

        await Service.Handle(cmd, cancellationToken);

        var secondCmd = new Commands.RecordPayment(new(cmd.BookingId), Auto.Create<string>(), new(cmd.Price), DateTimeOffset.Now);

        var expected = new Change[] {
            new(new BookingPaymentRegistered(secondCmd.PaymentId, secondCmd.Amount.Amount), TypeNames.PaymentRegistered),
            new(new BookingOutstandingAmountChanged(0), TypeNames.OutstandingAmountChanged),
            new(new BookingFullyPaid(secondCmd.PaidAt), TypeNames.BookingFullyPaid)
        };

        var result = await Service.Handle(secondCmd, cancellationToken);

        result.TryGet(out var ok).Should().BeTrue();
        ok!.Changes.Should().BeEquivalentTo(expected);

        var evt = await EventStore.ReadEvents(GetStreamName(new(cmd.BookingId)), StreamReadPosition.Start, 100, CancellationToken.None);

        var actual = evt.Skip(1).Select(x => x.Payload);

        actual.Should().BeEquivalentTo(expected.Select(x => x.Event));
    }

    static StreamName GetStreamName(BookingId bookingId) => new($"hotel-booking-{bookingId}");
}
