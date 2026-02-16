// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Shared.Generators;

/// <summary>
/// Constants used for type and member lookups.
/// These are primarily used for symbol resolution via Compilation.GetTypeByMetadataName()
/// and as fallback when symbol-based comparison is not available.
/// The generators now prefer symbol-based comparisons using SymbolEqualityComparer,
/// which are refactoring-safe and won't break when types are renamed.
/// </summary>
internal static class Constants {
    /// <summary>Base namespace for Eventuous types.</summary>
    public const string BaseNamespace = "Eventuous";

    /// <summary>Name of the EventType attribute class (without namespace).</summary>
    public const string EventTypeAttribute = "EventTypeAttribute";

    /// <summary>Fully qualified name of the EventType attribute for GetTypeByMetadataName().</summary>
    public const string EventTypeAttrFqcn = $"{BaseNamespace}.{EventTypeAttribute}";
}
