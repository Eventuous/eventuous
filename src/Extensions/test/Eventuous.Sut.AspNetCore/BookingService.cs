using Eventuous.AspNetCore.Web;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using NodaTime;

namespace Eventuous.Sut.AspNetCore;

using static SutBookingCommands;

public class BookingService : CommandService<Booking, BookingState, BookingId> {
    public BookingService(IAggregateStore store, ILogger<BookingService> log, StreamNameMap? streamNameMap = null)
        : base(store, streamNameMap: streamNameMap) {
        log.LogInformation("Instantiating Booking service");

        On<BookRoom>()
            .InState(ExpectedState.New)
            .GetId(cmd => new BookingId(cmd.BookingId))
            .Act(
                (booking, cmd)
                    => booking.BookRoom(
                        cmd.RoomId,
                        new StayPeriod(cmd.CheckIn, cmd.CheckOut),
                        new Money(cmd.Price),
                        cmd.GuestId
                    )
            );

        On<NestedCommands.NestedBookRoom>()
            .InState(ExpectedState.New)
            .GetId(cmd => new BookingId(cmd.BookingId))
            .Act(
                (booking, cmd)
                    => booking.BookRoom(
                        cmd.RoomId,
                        new StayPeriod(cmd.CheckIn, cmd.CheckOut),
                        new Money(cmd.Price),
                        cmd.GuestId
                    )
            );

        On<Commands.RecordPayment>()
            .InState(ExpectedState.Existing)
            .GetId(cmd => cmd.BookingId)
            .Act((booking, cmd) => booking.RecordPayment(cmd.PaymentId, cmd.Amount, cmd.PaidAt));

        On<ImportBooking>()
            .InState(ExpectedState.New)
            .GetId(cmd => cmd.BookingId)
            .Act((booking, cmd) => booking.Import(cmd.RoomId, cmd.Period, cmd.Price));
    }
}

public static class SutBookingCommands {
    public const string BookRoute       = "book";
    public const string NestedBookRoute = "nested-book";

    [HttpCommand(Route = BookRoute)]
    public record BookRoom(
            string    BookingId,
            string    RoomId,
            LocalDate CheckIn,
            LocalDate CheckOut,
            float     Price,
            string?   GuestId
        );

    public record ImportBooking(BookingId BookingId, string RoomId, StayPeriod Period, Money Price);

    [StateCommands<BookingState>]
    public static class NestedCommands {
        [HttpCommand(Route = NestedBookRoute)]
        public record NestedBookRoom(
                string    BookingId,
                string    RoomId,
                LocalDate CheckIn,
                LocalDate CheckOut,
                float     Price,
                string?   GuestId
            );
    }
}
