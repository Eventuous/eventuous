using System;

namespace Eventuous {
    public interface IEventSerializer {
        object? DeserializeEvent(ReadOnlySpan<byte> data, string eventType);

        (string EventType, byte[] Payload) SerializeEvent(object evt);
        
        byte[] SerializeMetadata(Metadata evt);

        string ContentType { get; }
    }
}