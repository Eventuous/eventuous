// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Sut.Domain;

public record Money {
    public Money(float amount, string currency = "EUR") {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive");

        Amount   = amount;
        Currency = currency;
    }

    public float  Amount   { get; init; }
    public string Currency { get; init; }

    public static Money operator +(Money left, Money right) {
        if (left.Currency != right.Currency) throw new InvalidOperationException("Currencies must match");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right) {
        if (left.Currency != right.Currency) throw new InvalidOperationException("Currencies must match");
        return new Money(left.Amount - right.Amount, left.Currency);
    }
}
