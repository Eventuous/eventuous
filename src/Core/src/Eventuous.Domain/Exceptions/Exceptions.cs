// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

static class Exceptions {
    public class InvalidIdException(Type idType) : Exception(ExceptionMessages.AggregateIdEmpty(idType)) {
        public InvalidIdException(Id id)
            : this(id.GetType()) { }
    }

    public class InvalidIdException<T>() : InvalidIdException(typeof(T)) where T : Id;

    internal class DuplicateTypeException<T>() : ArgumentException(ExceptionMessages.DuplicateTypeKey<T>(), typeof(T).FullName);
}
