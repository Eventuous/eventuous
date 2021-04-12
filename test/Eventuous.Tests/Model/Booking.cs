using System.Collections.Immutable;
using System.Linq;
using static Eventuous.Tests.Model.BookingEvents;

namespace Eventuous.Tests.Model {
    public class Booking : Aggregate<BookingState, BookingId> {
        public void BookRoom(BookingId id, string roomId, StayPeriod period, decimal price) {
            EnsureDoesntExist();

            Apply(new RoomBooked(id, roomId, period.CheckIn, period.CheckOut, price));
        }

        public void Import(BookingId id, string roomId, StayPeriod period) {
            EnsureDoesntExist();

            Apply(new BookingImported(id, roomId, period.CheckIn, period.CheckOut));
        }

        public void RecordPayment(string paymentId, decimal amount) {
            EnsureExists();
            
            if (State.HasPaymentRecord(paymentId)) return;

            var (previousState, currentState) = Apply(new BookingPaymentRegistered(State.Id, paymentId, amount));

            if (!previousState.IsFullyPaid() && currentState.IsFullyPaid())
                Apply(new BookingFullyPaid(State.Id));
        }
    }

    public record BookingState : AggregateState<BookingState, BookingId> {
        decimal                      Price          { get; init; }
        decimal                      AmountPaid     { get; init; }
        ImmutableList<PaymentRecord> PaymentRecords { get; init; } = ImmutableList<PaymentRecord>.Empty;

        internal bool HasPaymentRecord(string paymentId) => PaymentRecords.Any(x => x.PaymentId == paymentId);

        internal bool IsFullyPaid() => AmountPaid >= Price;

        internal bool IsOverpaid() => AmountPaid > Price;

        public override BookingState When(object @event)
            => @event switch {
                RoomBooked booked        => this with { Id = new BookingId(booked.BookingId), Price = booked.Price },
                BookingImported imported => this with { Id = new BookingId(imported.BookingId) },
                BookingPaymentRegistered paid => this with {
                    PaymentRecords = PaymentRecords.Add(new PaymentRecord(paid.PaymentId, paid.AmountPaid)),
                    AmountPaid = AmountPaid + paid.AmountPaid
                },
                _ => this
            };

        record PaymentRecord(string PaymentId, decimal Amount);
    }

    public record BookingId(string Value) : AggregateId(Value);
}