// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public readonly record struct ExpectedStreamVersion(long Value) {
    public static readonly ExpectedStreamVersion NoStream = new(-1);
    public static readonly ExpectedStreamVersion Any      = new(-2);

    public bool ExistingStream => Value >= 0;
}

public record struct StreamReadPosition {
    public StreamReadPosition(long Value) {
        if (Value < 0) throw new ArgumentOutOfRangeException(nameof(Value), "StreamReadPosition cannot be negative.");

        this.Value = Value;
    }

    public static readonly StreamReadPosition Start = new(0L);
    public static readonly StreamReadPosition End   = new(long.MaxValue);
    public static implicit operator StreamReadPosition(long value) => new(value);
    public long Value { get; set; }

    public readonly void Deconstruct(out long value) => value = this.Value;
}

public record struct StreamTruncatePosition(long Value);
