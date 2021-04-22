namespace Eventuous {
    public record StreamEvent(string EventType, byte[] Data, byte[]? Metadata, string ContentType);
}