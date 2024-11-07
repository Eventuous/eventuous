// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous; 

static class Exceptions {
    internal class DuplicateTypeException<T>() : ArgumentException(ExceptionMessages.DuplicateTypeKey<T>(), typeof(T).FullName);
}