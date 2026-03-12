// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Shared.Generators;

internal static class Helpers {
    public static string MakeGlobal(string typeName) => !typeName.StartsWith("global::") ? $"global::{typeName}" : typeName;
}
