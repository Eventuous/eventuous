// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public record AppendEventsResult(ulong GlobalPosition, long NextExpectedVersion) {
    public static readonly AppendEventsResult NoOp = new(0, -1);
}
