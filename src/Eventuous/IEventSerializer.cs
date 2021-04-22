using System;

namespace Eventuous {
    public interface IEventSerializer {
        object? Deserialize(ReadOnlySpan<byte> data, string eventType);

        byte[] Serialize(object evt);

        string ContentType { get; }
    }
}