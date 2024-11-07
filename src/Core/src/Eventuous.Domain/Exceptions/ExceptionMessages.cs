// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using System.Resources;

namespace Eventuous;

static class ExceptionMessages {
    static readonly ResourceManager Resources = new("Eventuous.ExceptionMessages", Assembly.GetExecutingAssembly());

    internal static string AggregateIdEmpty(Type idType)
        => string.Format(Resources.GetString("AggregateIdEmpty")!, idType.Name);

    internal static string DuplicateTypeKey<T>()
        => string.Format(Resources.GetString("DuplicateTypeKey")!, typeof(T).Name);
}