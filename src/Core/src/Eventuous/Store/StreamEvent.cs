namespace Eventuous; 

[PublicAPI]
public record StreamEvent(Guid Id, object? Payload, Metadata Metadata, string ContentType, long Position);