# Source Generator Event Type Detection Fixes

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix two source generators so they detect event types registered via `EventHandler.On<T>()` and similar patterns, and add `[EventType]` attribute discovery as a fallback for indirect registration patterns.

**Architecture:** Three changes across two generator files. Change 1 adds `EventHandler.On<T>()` detection to the `EventUsageAnalyzer` diagnostic. Change 2 replaces the name-based heuristic in `ConsumeContextConverterGenerator` with a containing-type check. Change 3 adds `[EventType]` attribute discovery from current and referenced assemblies to the converter generator.

**Tech Stack:** Roslyn source generators (IIncrementalGenerator), Roslyn analyzers (DiagnosticAnalyzer), Microsoft.CodeAnalysis.CSharp, netstandard2.0

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `src/Core/gen/Eventuous.Shared.Generators/EventUsageAnalyzer.cs` | Modify | Add `BaseEventHandler` to `KnownTypeSymbols`, add `IsEventHandler()`, add case 1d |
| `src/Core/gen/Eventuous.Subscriptions.Generators/ConsumeContextConverterGenerator.cs` | Modify | Replace name heuristic with containing-type check, add `[EventType]` discovery path |
| `src/Core/test/Eventuous.Tests.Shared.Analyzers/Analyzed.cs` | Modify | Add `EventHandler.On<T>()` test fixture |
| `src/Core/test/Eventuous.Tests.Shared.Analyzers/Analyzer_Ev001_Tests.cs` | Modify | Add test for EventHandler case |
| `src/Core/test/Eventuous.Tests.Shared.Analyzers/Eventuous.Tests.Shared.Analyzers.csproj` | Modify | Add Subscriptions project reference |

---

### Task 1: Add `EventHandler.On<T>()` detection to `EventUsageAnalyzer`

**Files:**
- Modify: `src/Core/gen/Eventuous.Shared.Generators/EventUsageAnalyzer.cs:49-58` (KnownTypeSymbols)
- Modify: `src/Core/gen/Eventuous.Shared.Generators/EventUsageAnalyzer.cs:142-151` (add case 1d)
- Modify: `src/Core/gen/Eventuous.Shared.Generators/EventUsageAnalyzer.cs:270-290` (add IsEventHandler)

- [ ] **Step 1: Add `BaseEventHandler` to `KnownTypeSymbols`**

In `EventUsageAnalyzer.cs`, add to the `KnownTypeSymbols` class (after line 57):

```csharp
public INamedTypeSymbol? BaseEventHandler { get; } = compilation.GetTypeByMetadataName("Eventuous.Subscriptions.BaseEventHandler");
```

- [ ] **Step 2: Add `IsEventHandler()` helper**

After the `IsState()` method (after line 290), add:

```csharp
static bool IsEventHandler(INamedTypeSymbol? type, KnownTypeSymbols knownTypes) {
    if (type == null) return false;

    for (var t = type; t != null; t = t.BaseType) {
        if (knownTypes.BaseEventHandler != null) {
            if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, knownTypes.BaseEventHandler)) {
                return true;
            }
        }
        else {
            if (t is { Name: "BaseEventHandler", Arity: 0 } && t.ContainingNamespace?.ToDisplayString() == "Eventuous.Subscriptions") {
                return true;
            }
        }
    }

    return false;
}
```

- [ ] **Step 3: Add case 1d in the method switch**

In `AnalyzeInvocation`, after case 1c (after line 151, before the closing `}`  of the switch on line 152), add:

```csharp
// Case 1d: EventHandler.On<T>(...) handler registrations
case { Name: "On", TypeArguments.Length: 1 } when IsEventHandler(method.ContainingType, knownTypes): {
    var eventType = method.TypeArguments[0];

    if (IsConcreteEvent(eventType) && !HasEventTypeAttribute(eventType, knownTypes) && !IsExplicitlyRegistered(eventType, ctx, knownTypes)) {
        ctx.ReportDiagnostic(Diagnostic.Create(MissingEventTypeAttribute, inv.Syntax.GetLocation(), eventType.ToDisplayString()));
    }

    return;
}
```

- [ ] **Step 4: Build the generator project**

Run: `dotnet build src/Core/gen/Eventuous.Shared.Generators/Eventuous.Shared.Generators.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/Core/gen/Eventuous.Shared.Generators/EventUsageAnalyzer.cs
git commit -m "fix(analyzers): detect EventHandler.On<T>() for missing EventType warning"
```

---

### Task 2: Add analyzer test for `EventHandler.On<T>()` detection

**Files:**
- Modify: `src/Core/test/Eventuous.Tests.Shared.Analyzers/Eventuous.Tests.Shared.Analyzers.csproj`
- Modify: `src/Core/test/Eventuous.Tests.Shared.Analyzers/Analyzed.cs`
- Modify: `src/Core/test/Eventuous.Tests.Shared.Analyzers/Analyzer_Ev001_Tests.cs`

- [ ] **Step 1: Add Subscriptions project reference**

In `Eventuous.Tests.Shared.Analyzers.csproj`, add after the existing `ProjectReference` entries (after line 21):

```xml
<ProjectReference Include="$(LocalRoot)\Eventuous.Subscriptions\Eventuous.Subscriptions.csproj"/>
```

- [ ] **Step 2: Add EventHandler fixture to `Analyzed.cs`**

Add the following after the existing `Events` class (after line 23):

```csharp
file class TestEventHandler : Eventuous.Subscriptions.EventHandler {
    public TestEventHandler() {
        On<Events.RoomBooked>(ctx => new ValueTask());
    }
}
```

- [ ] **Step 3: Add metadata reference in `Analyzer_Ev001_Tests.cs`**

In the `CreateCompilation` method, add to the `refs` list (after line 56):

```csharp
MetadataReference.CreateFromFile(typeof(Eventuous.Subscriptions.EventHandler).Assembly.Location),
```

- [ ] **Step 4: Update test assertion to expect 3 diagnostics**

In `Should_warn_for_unannotated_events_in_state_and_aggregate`, update the assertion at line 29:

```csharp
await Assert.That(ev001.Length).IsGreaterThanOrEqualTo(3);
```

- [ ] **Step 5: Run the test**

Run: `dotnet test src/Core/test/Eventuous.Tests.Shared.Analyzers/Eventuous.Tests.Shared.Analyzers.csproj --filter "FullyQualifiedName~Analyzer_Ev001_Tests" -f net10.0`
Expected: PASS — 3 EV001 diagnostics including one for `EventHandler.On<RoomBooked>`

- [ ] **Step 6: Commit**

```bash
git add src/Core/test/Eventuous.Tests.Shared.Analyzers/
git commit -m "test(analyzers): add EventHandler.On<T>() detection test"
```

---

### Task 3: Replace name heuristic in `ConsumeContextConverterGenerator`

**Files:**
- Modify: `src/Core/gen/Eventuous.Subscriptions.Generators/ConsumeContextConverterGenerator.cs`

- [ ] **Step 1: Add `BaseEventHandler` symbol resolution to the pipeline**

In `Initialize()`, add a second symbol resolution alongside the existing `messageConsumeContextSymbol` (after line 20):

```csharp
var baseEventHandlerSymbol = context.CompilationProvider
    .Select(static (c, _) => c.GetTypeByMetadataName("Eventuous.Subscriptions.BaseEventHandler"));
```

- [ ] **Step 2: Combine both symbols into the pipeline**

Replace the existing pipeline (lines 22-29) to thread both symbols through. Change the `.Combine(messageConsumeContextSymbol)` to combine both:

```csharp
var knownSymbols = messageConsumeContextSymbol
    .Combine(baseEventHandlerSymbol)
    .Select(static (pair, _) => new KnownSymbols(pair.Left, pair.Right));

var candidateTypes = context.SyntaxProvider
    .CreateSyntaxProvider(IsPotentialUsage, Transform)
    .Where(static t => t is not null)
    .Combine(knownSymbols)
    .Select(static (pair, _) => TransformWithSymbol(pair.Left, pair.Right))
    .Where(static t => t is not null)
    .Select(static (t, _) => t!)
    .Collect();
```

- [ ] **Step 3: Add `KnownSymbols` record**

Add inside the class (e.g., after line 15):

```csharp
sealed record KnownSymbols(INamedTypeSymbol? MessageConsumeContext, INamedTypeSymbol? BaseEventHandler);
```

- [ ] **Step 4: Update `TransformWithSymbol` signature**

Change the signature from:

```csharp
static string? TransformWithSymbol(GeneratorSyntaxContext? ctx, INamedTypeSymbol? messageConsumeContextSymbol)
```

to:

```csharp
static string? TransformWithSymbol(GeneratorSyntaxContext? ctx, KnownSymbols known)
```

Update all references to `messageConsumeContextSymbol` inside the method to `known.MessageConsumeContext`. Pass `known.BaseEventHandler` to the `On<T>` check.

- [ ] **Step 5: Replace `ShouldTreatGenericOnAsEvent`**

Replace the existing method (lines 135-143) with:

```csharp
static bool IsEventHandlerOnMethod(IMethodSymbol method, INamedTypeSymbol? baseEventHandlerSymbol) {
    if (method is not { Name: "On" }) return false;
    var def = method.OriginalDefinition;
    if (def.TypeParameters.Length != 1) return false;

    var containingType = def.ContainingType;

    for (var t = containingType; t != null; t = t.BaseType) {
        if (baseEventHandlerSymbol != null) {
            if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, baseEventHandlerSymbol)) {
                return true;
            }
        }
        else {
            if (t is { Name: "BaseEventHandler", Arity: 0 } && t.ContainingNamespace?.ToDisplayString() == "Eventuous.Subscriptions") {
                return true;
            }
        }
    }

    return false;
}
```

- [ ] **Step 6: Update the call site in `TransformWithSymbol`**

In Case 2 (around line 75), change:

```csharp
if (method?.TypeArguments.Length == 1 && ShouldTreatGenericOnAsEvent(method)) {
```

to:

```csharp
if (method?.TypeArguments.Length == 1 && IsEventHandlerOnMethod(method, known.BaseEventHandler)) {
```

- [ ] **Step 7: Build the generator project**

Run: `dotnet build src/Core/gen/Eventuous.Subscriptions.Generators/Eventuous.Subscriptions.Generators.csproj`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add src/Core/gen/Eventuous.Subscriptions.Generators/ConsumeContextConverterGenerator.cs
git commit -m "fix(generators): replace name heuristic with containing-type check for On<T>"
```

---

### Task 4: Add `[EventType]` discovery path to `ConsumeContextConverterGenerator`

**Files:**
- Modify: `src/Core/gen/Eventuous.Subscriptions.Generators/ConsumeContextConverterGenerator.cs`

- [ ] **Step 1: Add `EventTypeAttribute` symbol resolution**

In `Initialize()`, add after the `baseEventHandlerSymbol` resolution:

```csharp
var eventTypeAttributeSymbol = context.CompilationProvider
    .Select(static (c, _) => c.GetTypeByMetadataName("Eventuous.EventTypeAttribute"));
```

- [ ] **Step 2: Add the `[EventType]` discovery pipeline**

After the existing `candidateTypes` pipeline, add:

```csharp
var eventTypeCandidates = eventTypeAttributeSymbol
    .Combine(context.CompilationProvider)
    .Select(static (pair, _) => DiscoverEventTypes(pair.Right, pair.Left));
```

- [ ] **Step 3: Merge both candidate sources**

Replace the `context.RegisterSourceOutput(candidateTypes, Generate);` line with:

```csharp
var mergedCandidates = candidateTypes
    .Combine(eventTypeCandidates)
    .Select(static (pair, _) => pair.Left.AddRange(pair.Right));

context.RegisterSourceOutput(mergedCandidates, Generate);
```

- [ ] **Step 4: Add `DiscoverEventTypes` method**

Add after the `Generate` method:

```csharp
static ImmutableArray<string> DiscoverEventTypes(Compilation compilation, INamedTypeSymbol? eventTypeAttributeSymbol) {
    if (eventTypeAttributeSymbol is null) return ImmutableArray<string>.Empty;

    var builder = ImmutableArray.CreateBuilder<string>();

    ProcessNamespace(compilation.Assembly.GlobalNamespace);

    foreach (var ra in compilation.SourceModule.ReferencedAssemblySymbols) {
        ProcessNamespace(ra.GlobalNamespace);
    }

    return builder.ToImmutable();

    void ProcessType(INamedTypeSymbol type) {
        if (HasEventTypeAttribute(type)) {
            var name = GetTypeSyntax(type);
            if (name is not null) builder.Add(name);
        }

        foreach (var nt in type.GetTypeMembers()) {
            ProcessType(nt);
        }
    }

    void ProcessNamespace(INamespaceSymbol ns) {
        foreach (var member in ns.GetMembers()) {
            switch (member) {
                case INamespaceSymbol cns:
                    ProcessNamespace(cns);
                    break;
                case INamedTypeSymbol type:
                    ProcessType(type);
                    break;
            }
        }
    }

    bool HasEventTypeAttribute(INamedTypeSymbol type) =>
        type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, eventTypeAttributeSymbol));
}
```

- [ ] **Step 5: Build the generator project**

Run: `dotnet build src/Core/gen/Eventuous.Subscriptions.Generators/Eventuous.Subscriptions.Generators.csproj`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add src/Core/gen/Eventuous.Subscriptions.Generators/ConsumeContextConverterGenerator.cs
git commit -m "fix(generators): add EventType attribute discovery for consume context converters"
```

---

### Task 5: Verify full solution builds and existing tests pass

**Files:** None (verification only)

- [ ] **Step 1: Build full solution**

Run: `dotnet build Eventuous.slnx`
Expected: Build succeeded

- [ ] **Step 2: Run analyzer tests**

Run: `dotnet test src/Core/test/Eventuous.Tests.Shared.Analyzers/Eventuous.Tests.Shared.Analyzers.csproj -f net10.0`
Expected: All tests pass

- [ ] **Step 3: Run existing context conversion tests**

Run: `dotnet test src/Mongo/test/Eventuous.Tests.Projections.MongoDB/Eventuous.Tests.Projections.MongoDB.csproj --filter "FullyQualifiedName~ContextConversions" -f net10.0`
Expected: All tests pass

- [ ] **Step 4: Commit (if any fixups needed)**

Only if build or tests required adjustments.
