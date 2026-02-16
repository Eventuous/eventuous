// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Spyglass.Generators;

internal static class Constants {
    public const string BaseNamespace = "Eventuous";
    public const string AggregateFqn  = $"{BaseNamespace}.Aggregate<T>";
    public const string StateFqn      = $"{BaseNamespace}.State<T>";
    public const string OnMethodName  = "On";
}
