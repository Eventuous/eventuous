// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Tests.Application;

using Sut.Domain;
using static Sut.App.Commands;
using static Sut.Domain.BookingEvents;

[Obsolete("Obsolete")]
public class BookingFuncService : FunctionalCommandService<BookingState> {
    public BookingFuncService(IEventStore store, TypeMapper? typeMap = null, AmendEvent? amendEvent = null) : base(store, typeMap, amendEvent) {
        // Keep it for tests until the old API is gone
        OnNew<BookRoom>(cmd => GetStream(cmd.BookingId), BookRoom);
        On<RecordPayment>().InState(ExpectedState.Existing).GetStream(cmd => GetStream(cmd.BookingId)).Act(RecordPayment);
        On<ImportBooking>().InState(ExpectedState.Any).GetStream(cmd => GetStream(cmd.BookingId)).Act(ImportBooking);

        return;

        static IEnumerable<object> BookRoom(BookRoom cmd) => [new RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price)];

        static IEnumerable<object> ImportBooking(BookingState state, object[] events, ImportBooking cmd)
            => [new BookingImported(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut)];

        static IEnumerable<object> RecordPayment(BookingState state, object[] originalEvents, RecordPayment cmd) {
            if (state.HasPayment(cmd.PaymentId)) yield break;

            var registered = new BookingPaymentRegistered(cmd.PaymentId, cmd.Amount.Amount);

            yield return registered;

            var newState = state.When(registered);

            if (newState.IsFullyPaid()) yield return new BookingFullyPaid(cmd.PaidAt);
            if (newState.IsOverpaid()) yield return new BookingOverpaid((state.AmountPaid - state.Price).Amount);
        }
    }
}
