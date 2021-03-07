using System;

namespace CoreLib {
    public interface IEventSerializer {
        object Deserialize(ReadOnlySpan<byte> data, string eventType);

        byte[] Serialize(object evt);
    }
}