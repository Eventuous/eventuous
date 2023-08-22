// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Sut.Domain;

public record Money(float Amount, string Currency = "EUR") {
    public static Money operator +(Money left, Money right) {
        if (left.Currency != right.Currency) throw new InvalidOperationException("Currencies must match");
        return left with { Amount = left.Amount + right.Amount };
    }

    public static Money operator -(Money left, Money right) {
        if (left.Currency != right.Currency) throw new InvalidOperationException("Currencies must match");
        return left with { Amount = left.Amount - right.Amount };
    }
}
