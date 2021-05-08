using System;
using System.Collections;
using System.Collections.Generic;

namespace Eventuous.Subscriptions {
    public class DeserializationException : Exception {
        public DeserializationException(ReceivedEvent re, Exception e)
            : base($"Error deserializing event {re.OriginalStream} {re.StreamPosition} {re.EventType}", e) {
            Data.Add("ReceivedEvent", re);
        }

        public sealed override IDictionary Data { get; } = new Dictionary<string, object>();
    }

    public class SubscriptionException : Exception {
        public SubscriptionException(ReceivedEvent re, object? evt, Exception e)
            : base($"Error processing event {re.OriginalStream} {re.StreamPosition} {re.EventType}", e) {
            Data.Add("ReceivedEvent", re);
            Data.Add("Event", evt);
        }

        public sealed override IDictionary Data { get; } = new Dictionary<string, object>();
    }
}