namespace Eventuous; 

[PublicAPI]
public record StreamEvent(object? Payload, Metadata Metadata, string ContentType, long Position);