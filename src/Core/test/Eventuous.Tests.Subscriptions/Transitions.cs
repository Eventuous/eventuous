// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Drops and resubscriptions as the subscription's own callbacks report them. Written by the supervisor,
/// read by the test — hence the volatile and the interlocked.
/// </summary>
sealed class Transitions {
    volatile bool _up;
    int           _drops;

    /// <summary>
    /// True when the last transition was coming up rather than a drop.
    /// </summary>
    public bool Up => _up;

    public int Drops => Volatile.Read(ref _drops);

    public void Subscribed() => _up = true;

    public void Dropped() {
        _up = false;
        Interlocked.Increment(ref _drops);
    }
}
