// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections;

namespace Eventuous.Subscriptions;

public class DeserializationException : Exception {
    public DeserializationException(string stream, string eventType, ulong position, Exception e)
        : base($"Error deserializing event {stream} {position} {eventType}", e) { }

    public DeserializationException(string stream, string eventType, ulong position, string message)
        : base($"Error deserializing event {stream} {position} {eventType}: {message}") { }
}

public class SubscriptionException : Exception {
    public SubscriptionException(string stream, string eventType, object? evt, Exception e) : base($"Error processing event {stream} {eventType}", e)
        => Data.Add("Event", evt);

    public sealed override IDictionary Data { get; } = new Dictionary<string, object>();
}
