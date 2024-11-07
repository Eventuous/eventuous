// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Options for <see cref="StreamSubscription"/>
/// </summary>
public record StreamSubscriptionOptions : CatchUpSubscriptionOptions {
    /// <summary>
    /// WHen set to true, all events of type that starts with '$' will be ignored. Default is true.
    /// </summary>
    public bool IgnoreSystemEvents { get; set; } = true;

    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName StreamName { get; set; }
}