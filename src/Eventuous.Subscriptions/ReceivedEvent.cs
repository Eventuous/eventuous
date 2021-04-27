using System;

namespace Eventuous.Subscriptions {
    public class ReceivedEvent {
        public string               EventId        { get; init; }
        public string               EventType      { get; init; }
        public string               ContentType    { get; init; }
        public ulong                GlobalPosition { get; init; }
        public ulong                StreamPosition { get; init; }
        public ulong                Sequence       { get; init; }
        public DateTime             Created        { get; init; }
        public ReadOnlyMemory<byte> Data           { get; init; }
        public ReadOnlyMemory<byte> Metadata       { get; init; }
    }
}