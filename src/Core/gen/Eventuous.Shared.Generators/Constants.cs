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
    public const string StateType = "State";

    public const string BaseNamespace = "Eventuous";

    public const string EventTypeAttribute = "EventTypeAttribute";
    public const string EventTypeAttrFqcn  = $"{BaseNamespace}.{EventTypeAttribute}";

    public const string SnapshotsAttribute = "SnapshotsAttribute";
    public const string SnapshotsAttrFqcn  = $"{BaseNamespace}.{SnapshotsAttribute}";

    public const string SnapshotSameStream     = nameof(SnapshotStorageStrategy.SameStream);
    public const string SnapshotSeparateStream = nameof(SnapshotStorageStrategy.SeparateStream);
    public const string SnapshotSeparateStore  = nameof(SnapshotStorageStrategy.SeparateStore);
}
