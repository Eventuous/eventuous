namespace Eventuous;

public static class Exceptions {
    public class InvalidIdException : Exception {
        public InvalidIdException(AggregateId id) : base(
            $"Aggregate id {id.GetType().Name} cannot have an empty value"
        ) { }
    }

    public class AggregateNotFound<T> : Exception {
        public AggregateNotFound(string id, Exception? inner) : base(
            $"Aggregate of type {typeof(T).Name} with id {id} does not exist",
            inner
        ) { }
    }

    public class CommandHandlerNotFound : Exception {
        public CommandHandlerNotFound(Type type) : base($"Handler not found for command {type.Name}") { }
    }
}

public static class EventStoreExceptions {
    public class StreamNotFound : Exception {
        public StreamNotFound(string stream) : base($"Stream {stream} does not exist") { }
    }

    public class AppendToStreamException : Exception {
        public AppendToStreamException(string stream, Exception inner)
            : base($"Unable to append events to {stream}", inner) { }
    }

    public class ReadFromStreamException : Exception {
        public ReadFromStreamException(string stream, Exception inner)
            : base($"Unable to read events from {stream}", inner) { }
    }

    public class DeleteStreamException : Exception {
        public DeleteStreamException(string stream, Exception inner)
            : base($"Unable to delete stream {stream}", inner) { }
    }

    public class TruncateStreamException : Exception {
        public TruncateStreamException(string stream, Exception inner)
            : base($"Unable to truncate stream {stream}", inner) { }
    }
}

public class DomainException : Exception {
    public DomainException(string message) : base(message) { }
}