// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions;

[Flags]
public enum EventHandlingStatus : short {
    Ignored = 0b_1000,
    Success = 0b_0001,
    Pending = 0b_0010,
    Failure = 0b_0011,
    Handled = 0b_0111
    // 0111 bitmask for Handled means that if any of the three lower bits is set, the message
    // hs been handled.
}