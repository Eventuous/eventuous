// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous; 

static class Exceptions {
    internal class DuplicateTypeException<T> : ArgumentException {
        public DuplicateTypeException() : base(ExceptionMessages.DuplicateTypeKey<T>(), typeof(T).FullName) { }
    }
}