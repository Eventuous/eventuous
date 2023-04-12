// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public record ExpectedStreamVersion(long Value) {
    public static readonly ExpectedStreamVersion NoStream = new(-1);
    public static readonly ExpectedStreamVersion Any      = new(-2);
}

public record StreamReadPosition(long Value) {
    public static readonly StreamReadPosition Start = new(0L);
}

public record StreamTruncatePosition(long Value);