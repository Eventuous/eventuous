// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sut.App;
using Eventuous.Sut.Domain;

namespace ElasticPlayground;

public static class MiscExtensions {
    public static Commands.RecordPayment ToRecordPayment(this Commands.BookRoom command, string paymentId, float divider = 1)
        => new(
            new BookingId(command.BookingId),
            paymentId,
            new Money(command.Price / divider),
            DateTimeOffset.Now
        );
}
