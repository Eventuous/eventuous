// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public readonly record struct ExpectedStreamVersion(long Value) {
    public static readonly ExpectedStreamVersion NoStream = new(-1);
    public static readonly ExpectedStreamVersion Any      = new(-2);
}

public record struct StreamReadPosition(long Value) {
    public static readonly StreamReadPosition Start = new(0L);
    public static readonly StreamReadPosition End   = new(long.MaxValue);
}

public record struct StreamTruncatePosition(long Value);