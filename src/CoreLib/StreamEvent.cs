namespace CoreLib {
    public record StreamEvent(string EventType, byte[] Data, byte[]? Metadata = null);
}