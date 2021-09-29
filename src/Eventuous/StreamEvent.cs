using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public record StreamEvent(string EventType, byte[] Data, byte[]? Metadata, string ContentType, long Position);
}