using System;

namespace Eventuous.Subscriptions {
    public class ReceivedMessage {
        public Guid                 MessageId   { get; init; }
        public string               MessageType { get; init; }
        public ulong                Position    { get; init; }
        public ulong                Sequence    { get; init; }
        public DateTime             Created     { get; init; }
        public ReadOnlyMemory<byte> Data        { get; init; }
        public ReadOnlyMemory<byte> Metadata    { get; init; }
    }
}