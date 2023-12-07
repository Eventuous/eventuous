// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Serialization;
using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.AspNetCore.Web;
using static Bookings.Payments.Application.PaymentCommands;
using static Bookings.Payments.Domain.PaymentEvents;

namespace Bookings.Payments.Application;

public class CommandService : FunctionalCommandService<Payment> {
    public CommandService(IEventStore store) : base(store) {
        On<RecordPayment>()
            .InState(ExpectedState.New)
            .GetDefaultStream(c => c.PaymentId)
            .Act(RecordPayment);

        return;

        static IEnumerable<object> RecordPayment(Payment state, object[] originalEvents, RecordPayment cmd) {
            yield return new PaymentRecorded(
                cmd.PaymentId,
                cmd.BookingId,
                cmd.Amount,
                cmd.Currency,
                cmd.Method,
                cmd.Provider
            );
        }
    }
}

public static class PaymentCommands {
    [HttpCommand]
    public record RecordPayment(
            string                        PaymentId,
            string                        BookingId,
            float                         Amount,
            string                        Currency,
            string                        Method,
            string                        Provider,
            [property: JsonIgnore] string PaidBy
        );
}
