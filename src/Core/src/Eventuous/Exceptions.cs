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

    public class StreamNotFound : Exception {
        public StreamNotFound(string stream) : base($"Stream {stream} does not exist") { }
    }
}

public class DomainException : Exception {
    public DomainException(string message) : base(message) { }
}