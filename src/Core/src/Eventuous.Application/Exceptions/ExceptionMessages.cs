// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using System.Resources;

namespace Eventuous;

static class ExceptionMessages {
    static readonly ResourceManager Resources = new("Eventuous.ExceptionMessages", Assembly.GetExecutingAssembly());

    internal static string MissingCommandHandler(Type type) => string.Format(Resources.GetString("MissingCommandHandler")!, type.Name);

    internal static string DuplicateTypeKey<T>() => string.Format(Resources.GetString("DuplicateTypeKey")!, typeof(T).Name);

    internal static string DuplicateCommandHandler<T>() => string.Format(Resources.GetString("DuplicateCommandHandler")!, typeof(T).Name);

    internal static string MissingCommandMap<TIn, TOut>() => string.Format(Resources.GetString("MissingCommandMap")!, typeof(TIn).Name, typeof(TOut).Name);
}
