using static Eventuous.Tests.Model.BookingEvents;

namespace Eventuous.Tests.Model {
    public class Booking : Aggregate<BookingState, BookingId> {
        public void BookRoom(BookingId id, string roomId, StayPeriod period, decimal price) {
            EnsureDoesntExist();

            Apply(new RoomBooked(id, roomId, period.CheckIn, period.CheckOut, price));
        }
    }

    public record BookingState : AggregateState<BookingState, BookingId> {
        decimal Price { get; init; }

        public override BookingState When(object @event)
            => @event switch {
                RoomBooked booked => this with { Id = new BookingId(booked.BookingId), Price = booked.Price },
                _                 => this
            };
    }

    public record BookingId(string Value) : AggregateId(Value);
}