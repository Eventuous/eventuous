// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static ExceptionMessages;

public static class Exceptions {
    public class CommandHandlerNotFound : Exception {
        public CommandHandlerNotFound(Type type) : base(MissingCommandHandler(type)) { }
    }

    public class UnableToResolveAggregateId : Exception {
        public UnableToResolveAggregateId(Type type) :
            base($"Unable to resolve aggregate id from command {type.Name}") { }
    }

    public class CommandHandlerNotFound<T> : CommandHandlerNotFound {
        public CommandHandlerNotFound() : base(typeof(T)) { }
    }

    public class CommandHandlerAlreadyRegistered<T> : Exception {
        public CommandHandlerAlreadyRegistered() : base(DuplicateCommandHandler<T>()) { }
    }

    public class DuplicateTypeException<T> : ArgumentException {
        public DuplicateTypeException() : base(DuplicateTypeKey<T>(), typeof(T).FullName) { }
    }

    public class CommandMappingException<TIn, TOut> : Exception {
        public CommandMappingException() : base(MissingCommandMap<TIn, TOut>()) { }
    }
}
