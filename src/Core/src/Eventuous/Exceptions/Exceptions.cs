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

    public class CommandHandlerNotFound<T> : Exception {
        public CommandHandlerNotFound() :
            base($"Handler not found for command {typeof(T).Name}") { }
    }

    public class CommandHandlerAlreadyRegistered<T> : Exception {
        public CommandHandlerAlreadyRegistered() : base(
            $"Command handler for ${typeof(T).Name} already registered"
        ) { }
    }
}