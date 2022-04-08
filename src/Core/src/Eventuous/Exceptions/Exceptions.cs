namespace Eventuous;

public static class Exceptions {
    public class InvalidIdException : Exception {
        public InvalidIdException(AggregateId id) : this(id.GetType()) { }

        public InvalidIdException(Type idType) : base($"Aggregate id {idType.Name} cannot have an empty value") { }
    }

    public class InvalidIdException<T> : InvalidIdException where T : AggregateId {
        public InvalidIdException() : base(typeof(T)) { }
    }

    public class CommandHandlerNotFound : Exception {
        public CommandHandlerNotFound(Type type) :
            base($"Handler not found for command {type.Name}") { }
    }

    public class UnableToResolveAggregateId : Exception {
        public UnableToResolveAggregateId(Type type) :
            base($"Unable to resolve aggregate id from command {type.Name}") { }
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
