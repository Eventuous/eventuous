// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.SignalR.Client;

/// <summary>
/// Options for configuring <see cref="SignalRSubscriptionClient"/>.
/// </summary>
public class SignalRSubscriptionClientOptions {
    /// <summary>
    /// Optional event serializer. When not set, <see cref="EventSerializer.Default"/> is used.
    /// </summary>
    public IEventSerializer? Serializer { get; set; }

    /// <summary>
    /// Enables distributed tracing by propagating trace context from event metadata.
    /// </summary>
    public bool EnableTracing { get; set; }

    /// <summary>
    /// Maximum number of events buffered in the internal channel before applying back-pressure. Default is 1000.
    /// </summary>
    public int ChannelCapacity { get; set; } = 1000;
}
