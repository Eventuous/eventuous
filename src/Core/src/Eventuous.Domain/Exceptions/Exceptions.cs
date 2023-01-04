// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous; 

static class Exceptions {
    public class InvalidIdException : Exception {
        public InvalidIdException(AggregateId id) : this(id.GetType()) { }

        public InvalidIdException(Type idType) : base(ExceptionMessages.AggregateIdEmpty(idType)) { }
    }

    public class InvalidIdException<T> : InvalidIdException where T : AggregateId {
        public InvalidIdException() : base(typeof(T)) { }
    }

    internal class DuplicateTypeException<T> : ArgumentException {
        public DuplicateTypeException() : base(ExceptionMessages.DuplicateTypeKey<T>(), typeof(T).FullName) { }
    }
}