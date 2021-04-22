using System;

namespace Eventuous.Subscriptions {
    public class ReceivedEvent {
        public Guid                 EventId   { get; init; }
        public string               EventType { get; init; }
        public ulong                Position  { get; init; }
        public ulong                Sequence  { get; init; }
        public DateTime             Created   { get; init; }
        public ReadOnlyMemory<byte> Data      { get; init; }
        public ReadOnlyMemory<byte> Metadata  { get; init; }
    }
}