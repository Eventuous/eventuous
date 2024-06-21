// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

static class TypeExtensions {
    public static T? GetAttribute<T>(this Type type) where T : class => Attribute.GetCustomAttribute(type, typeof(T)) as T;
}