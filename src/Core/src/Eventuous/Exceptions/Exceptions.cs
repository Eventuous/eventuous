namespace Eventuous;

public static class Exceptions {
    public class InvalidIdException : Exception {
        public InvalidIdException(AggregateId id) : base(
            $"Aggregate id {id.GetType().Name} cannot have an empty value"
        ) { }
    }

    public class CommandHandlerNotFound : Exception {
        public CommandHandlerNotFound(Type type) :
            base($"Handler not found for command {type.Name}") { }
    }

    public class CommandHandlerNotFound<T> : CommandHandlerNotFound {
        public CommandHandlerNotFound() : base(typeof(T)) { }
    }

    public class CommandHandlerAlreadyRegistered<T> : Exception {
        public CommandHandlerAlreadyRegistered() : base(
            $"Command handler for ${typeof(T).Name} already registered"
        ) { }
    }
}