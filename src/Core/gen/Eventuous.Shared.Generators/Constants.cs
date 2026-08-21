// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Shared.Generators;

/// <summary>
/// Constants used for type and member lookups via Compilation.GetTypeByMetadataName().
/// The full list of metadata names the analyzers depend on lives in <see cref="WellKnownTypeNames"/>,
/// which is pinned by tests against the real Eventuous assemblies.
/// </summary>
internal static class Constants {
    /// <summary>Base namespace for Eventuous types.</summary>
    public const string BaseNamespace = "Eventuous";

    /// <summary>Name of the EventType attribute class (without namespace).</summary>
    public const string EventTypeAttribute = "EventTypeAttribute";

    /// <summary>Fully qualified name of the EventType attribute for GetTypeByMetadataName().</summary>
    public const string EventTypeAttrFqcn = $"{BaseNamespace}.{EventTypeAttribute}";
}
