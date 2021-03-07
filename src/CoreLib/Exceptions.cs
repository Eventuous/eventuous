using System;

namespace CoreLib {
    static class Exceptions {
        public class AggregateNotFound<T> : Exception {
            public AggregateNotFound(string id) : base($"Aggregate of type {typeof(T).Name} with id {id} does not exist") { }
        }

        public class CommandHandlerNotFound : Exception {
            public CommandHandlerNotFound(Type type) : base($"Handler not found for command {type.Name}") { }
        }
    }

    public class DomainException : Exception {
        public DomainException(string message) : base(message) { }
    }
}
