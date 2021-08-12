using System;
using AutoFixture;
using Eventuous.Tests.SutDomain;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace Eventuous.Tests {
    public class TwoAggregateOpsScenario {
        readonly Fixture _fixture = new();

        public TwoAggregateOpsScenario() {
            _id        = _fixture.Create<string>();
            _paymentId = _fixture.Create<string>();
            _amount    = _fixture.Create<decimal>();

            _booking.BookRoom(
                new BookingId(_id),
                _fixture.Create<string>(),
                new StayPeriod(
                    LocalDate.FromDateTime(DateTime.Today),
                    LocalDate.FromDateTime(DateTime.Today.AddDays(2))
                ),
                _amount
            );

            _booking.RecordPayment(
                _paymentId,
                _amount
            );
        }

        [Fact]
        public void should_produce_fully_paid_event() {
            var expected = new BookingEvents.BookingFullyPaid(_id);
            _booking.Changes.Should().Contain(expected);
        }

        [Fact]
        public void should_produce_payment_registered() {
            var expected = new BookingEvents.BookingPaymentRegistered(_id, _paymentId, _amount);
            _booking.Changes.Should().Contain(expected);
        }
        
        [Fact]
        public void should_make_booking_fully_paid() {
            _booking.State.IsFullyPaid().Should().BeTrue();
        }

        [Fact]
        public void should_record_payment_in_the_state() {
            _booking.State.HasPaymentRecord(_paymentId).Should().BeTrue();
        }

        [Fact]
        public void should_not_be_overpaid() {
            _booking.State.IsOverpaid().Should().BeFalse();
        }
        
        readonly string  _id;
        readonly Booking _booking = new();
        readonly decimal _amount;
        readonly string  _paymentId;
    }
}