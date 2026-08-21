// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;

namespace Eventuous.Shared.Generators;

/// <summary>
/// Fully qualified metadata names of the Eventuous types the analyzers resolve via
/// <see cref="Microsoft.CodeAnalysis.Compilation.GetTypeByMetadataName(string)"/>.
/// The analyzer cannot reference the runtime assemblies (it targets netstandard2.0, they don't),
/// so these strings are the single source of truth. Every name is pinned by a test that resolves
/// it against the current Eventuous assemblies, so renaming a type fails CI in the same change.
/// </summary>
public static class WellKnownTypeNames {
    public const string EventTypeAttribute      = Constants.EventTypeAttrFqcn;
    public const string TypeMapper              = $"{Constants.BaseNamespace}.TypeMapper";
    public const string Aggregate               = $"{Constants.BaseNamespace}.Aggregate`1";
    public const string State                   = $"{Constants.BaseNamespace}.State`1";
    public const string CommandHandlerBuilder   = $"{Constants.BaseNamespace}.CommandHandlerBuilder`2";
    public const string IDefineExecution        = $"{Constants.BaseNamespace}.IDefineExecution`2";
    public const string ICommandHandlerBuilder  = $"{Constants.BaseNamespace}.ICommandHandlerBuilder`2";
    public const string IDefineStoreOrExecution = $"{Constants.BaseNamespace}.IDefineStoreOrExecution`2";
    public const string BaseEventHandler        = "Eventuous.Subscriptions.BaseEventHandler";

    public static readonly ImmutableArray<string> All = [
        EventTypeAttribute,
        TypeMapper,
        Aggregate,
        State,
        CommandHandlerBuilder,
        IDefineExecution,
        ICommandHandlerBuilder,
        IDefineStoreOrExecution,
        BaseEventHandler
    ];
}
