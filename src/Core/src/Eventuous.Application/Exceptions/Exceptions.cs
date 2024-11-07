// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static ExceptionMessages;

public static class Exceptions {
    public class CommandHandlerNotFound(Type type) : Exception(MissingCommandHandler(type));

    public class CommandHandlerNotFound<T>() : CommandHandlerNotFound(typeof(T));

    public class CommandHandlerAlreadyRegistered<T>() : Exception(DuplicateCommandHandler<T>());

    public class DuplicateTypeException<T>() : ArgumentException(DuplicateTypeKey<T>(), typeof(T).FullName);

    public class MessageMappingException<TIn, TOut>() : Exception(MissingCommandMap<TIn, TOut>());
}
