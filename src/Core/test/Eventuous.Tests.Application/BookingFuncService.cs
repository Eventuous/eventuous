// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Application;

public class BookingFuncService : FunctionalCommandService<BookingState> {
    public BookingFuncService(IEventStore store, TypeMapper? typeMap = null) : base(store, typeMap) {
        OnNew<Commands.BookRoom>(cmd => GetStream(cmd.BookingId), BookRoom);
        OnExisting<Commands.RecordPayment>(cmd => GetStream(cmd.BookingId), RecordPayment);

        static StreamName GetStream(string id)
            => StreamName.For<Booking>(id);

        static IEnumerable<object> BookRoom(Commands.BookRoom cmd) {
            yield return new RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price);
        }

        static IEnumerable<object> RecordPayment(BookingState state, object[] originalEvents, Commands.RecordPayment cmd) {
            yield return new BookingPaymentRegistered(cmd.PaymentId, cmd.Amount.Amount);

            var paid        = state.AmountPaid + cmd.Amount;
            var outstanding = state.Price      - paid;
            if (outstanding.Amount <= 0) yield return new BookingFullyPaid(cmd.PaidAt);
            if (outstanding.Amount < 0) yield return new BookingOverpaid(-paid.Amount);
        }
    }
}
