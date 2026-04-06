# Source Generator Event Type Detection Fixes

**Issue:** [#538](https://github.com/Eventuous/eventuous/issues/538)
**Date:** 2026-04-06

## Problem

Two generators fail to detect event types registered via `EventHandler.On<T>()` and its derivatives:

1. **`EventUsageAnalyzer`** — warns about missing `[EventType]` for `State<T>.On<TEvent>()` but has no case for `EventHandler.On<T>()`. Events registered in handler subclasses get no diagnostic.

2. **`ConsumeContextConverterGenerator`** — uses a name-based heuristic (`ShouldTreatGenericOnAsEvent`) that requires the type parameter to contain "Event". Fails for `EventHandler.On<T>()`, `SqliteProjector.On<T>()`, `PostgresProjector.On<T>()`, `SqlServerProjector.On<T>()` — all use `T` not `TEvent`. Additionally, user-defined wrapper methods like `OnSessionOrAgent<T>()` that internally call `On<T>()` can never be detected through call-site tracing.

## Changes

### Change 1: Add `EventHandler.On<T>()` detection to `EventUsageAnalyzer`

**File:** `src/Core/gen/Eventuous.Shared.Generators/EventUsageAnalyzer.cs`

Add `BaseEventHandler` (or `EventHandler`) to `KnownTypeSymbols`:
```csharp
public INamedTypeSymbol? BaseEventHandler { get; } = compilation.GetTypeByMetadataName("Eventuous.Subscriptions.BaseEventHandler");
```

Add `IsEventHandler()` helper mirroring the existing `IsState()` pattern (lines 270-290) — walk the `ContainingType` base type chain checking against the resolved symbol, with string-based fallback.

Add a new case in `AnalyzeInvocation` alongside case 1c:
```csharp
// Case 1d: EventHandler.On<T>(...) handler registrations
case { Name: "On", TypeArguments.Length: 1 } when IsEventHandler(method.ContainingType, knownTypes):
```

Same diagnostic (`MissingEventTypeAttribute`) and same `IsExplicitlyRegistered` suppression logic.

### Change 2: Fix `ShouldTreatGenericOnAsEvent` in `ConsumeContextConverterGenerator`

**File:** `src/Core/gen/Eventuous.Subscriptions.Generators/ConsumeContextConverterGenerator.cs`

Replace the type-parameter-name heuristic with a containing-type check. Resolve `BaseEventHandler` symbol via `CompilationProvider` alongside the existing `IMessageConsumeContext` symbol. Pass it through the pipeline to `TransformWithSymbol`.

Replace `ShouldTreatGenericOnAsEvent` entirely — the type parameter name is irrelevant. The only check needed: is this an `On` method with 1 type argument whose containing type derives from `BaseEventHandler`? Walk `method.ContainingType.BaseType` chain against the resolved symbol, with string-based fallback. Remove all parameter name heuristics.

### Change 3: Add `[EventType]` discovery to `ConsumeContextConverterGenerator`

**File:** `src/Core/gen/Eventuous.Subscriptions.Generators/ConsumeContextConverterGenerator.cs`

Add a second discovery path using the same approach as `TypeMappingsGenerator.DiscoverFromCompilation` (lines 153-190 of `TypeMappingsGenerator.cs`):

- Resolve `EventTypeAttribute` symbol via `CompilationProvider`
- Walk `compilation.SourceModule.ReferencedAssemblySymbols` + current assembly global namespace
- Collect fully-qualified type names for all types with `[EventType]`

Merge with the existing syntax-based candidates before deduplication in `Generate()`. The pipeline mirrors `TypeMappingsGenerator`'s structure where `syntaxCandidates.Combine(symbolCandidates)` are merged.

This catches all event types regardless of how handlers are registered — direct `On<T>()`, wrapper methods, or any other indirection.

## Files Changed

| File | Change |
|------|--------|
| `EventUsageAnalyzer.cs` | Add `BaseEventHandler` to `KnownTypeSymbols`, add `IsEventHandler()`, add case 1d |
| `ConsumeContextConverterGenerator.cs` | Fix containing-type check in `ShouldTreatGenericOnAsEvent`, add `[EventType]` discovery path |

## Tradeoffs

- Change 3 may generate switch arms for `[EventType]` types from referenced assemblies that aren't consumed in this compilation. Cost is negligible — dead pattern match arms optimized by JIT.
- Changes 1 and 2 only improve direct `On<T>()` detection. Change 3 is what makes the converter generator complete for indirect patterns.
