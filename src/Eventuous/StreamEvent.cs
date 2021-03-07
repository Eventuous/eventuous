namespace Eventuous {
    public record StreamEvent(string EventType, byte[] Data, byte[]? Metadata = null);
}