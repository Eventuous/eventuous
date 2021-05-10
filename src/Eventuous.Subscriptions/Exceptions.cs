using System;
using System.Collections;
using System.Collections.Generic;

namespace Eventuous.Subscriptions {
    public class DeserializationException : Exception {
        public DeserializationException(string stream, string eventType, ulong position, Exception e)
            : base($"Error deserializing event {stream} {position} {eventType}", e) {
        }
    }

    public class SubscriptionException : Exception {
        public SubscriptionException(string stream, string eventType, ulong position, object? evt, Exception e)
            : base($"Error processing event {stream} {position} {eventType}", e) {
            Data.Add("Event", evt);
        }

        public sealed override IDictionary Data { get; } = new Dictionary<string, object>();
    }
}