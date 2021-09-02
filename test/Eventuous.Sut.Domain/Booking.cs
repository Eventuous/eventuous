using System.Collections.Immutable;
using System.Linq;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain {
    public class Booking : Aggregate<BookingState, BookingId> {
        public void BookRoom(BookingId id, string roomId, StayPeriod period, decimal price) {
            EnsureDoesntExist();

            Apply(new BookingEvents.RoomBooked(id, roomId, period.CheckIn, period.CheckOut, price));
        }

        public void Import(BookingId id, string roomId, StayPeriod period) {
            EnsureDoesntExist();

            Apply(new BookingEvents.BookingImported(id, roomId, period.CheckIn, period.CheckOut));
        }

        public void RecordPayment(string paymentId, decimal amount) {
            EnsureExists();
            
            if (State.HasPaymentRecord(paymentId)) return;

            var (previousState, currentState) = Apply(new BookingEvents.BookingPaymentRegistered(State.Id, paymentId, amount));

            if (!previousState.IsFullyPaid() && currentState.IsFullyPaid())
                Apply(new BookingEvents.BookingFullyPaid(State.Id));
        }
    }

    public record BookingState : AggregateState<BookingState, BookingId> {
        decimal                      Price          { get; init; }
        decimal                      AmountPaid     { get; init; }
        ImmutableList<PaymentRecord> PaymentRecords { get; init; } = ImmutableList<PaymentRecord>.Empty;

        public bool HasPaymentRecord(string paymentId) => PaymentRecords.Any(x => x.PaymentId == paymentId);

        public bool IsFullyPaid() => AmountPaid >= Price;

        public bool IsOverpaid() => AmountPaid > Price;

        public override BookingState When(object @event)
            => @event switch {
                BookingEvents.RoomBooked booked        => this with { Id = new BookingId(booked.BookingId), Price = booked.Price },
                BookingEvents.BookingImported imported => this with { Id = new BookingId(imported.BookingId) },
                BookingEvents.BookingPaymentRegistered paid => this with {
                    PaymentRecords = PaymentRecords.Add(new PaymentRecord(paid.PaymentId, paid.AmountPaid)),
                    AmountPaid = AmountPaid + paid.AmountPaid
                },
                _ => this
            };

        record PaymentRecord(string PaymentId, decimal Amount);
    }

    public record BookingId(string Value) : AggregateId(Value);
}