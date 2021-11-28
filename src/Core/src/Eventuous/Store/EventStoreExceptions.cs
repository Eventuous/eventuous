namespace Eventuous;

public abstract class EventStoreException : Exception {
    protected EventStoreException(string message, Exception inner) : base(message, inner) { }

    protected EventStoreException(string message) : base(message) { }
}

public class StreamNotFound : EventStoreException {
    public StreamNotFound(string stream) : base($"Stream {stream} does not exist") { }
}

public class AppendToStreamException : EventStoreException {
    public AppendToStreamException(string stream, Exception inner)
        : base($"Unable to append events to {stream}: {inner.Message}", inner) { }
}

public class ReadFromStreamException : EventStoreException {
    public ReadFromStreamException(string stream, Exception inner)
        : base($"Unable to read events from {stream}: {inner.Message}", inner) { }
}

public class DeleteStreamException : EventStoreException {
    public DeleteStreamException(string stream, Exception inner)
        : base($"Unable to delete stream {stream}: {inner.Message}", inner) { }
}

public class TruncateStreamException : EventStoreException {
    public TruncateStreamException(string stream, Exception inner)
        : base($"Unable to truncate stream {stream}: {inner.Message}", inner) { }
}