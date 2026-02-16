# Eventuous.Spyglass.Generators

A Roslyn incremental source generator that discovers Eventuous aggregates and states at compile time, replacing the previous reflection-based runtime discovery.

## What it does

The generator scans the compilation (including referenced assemblies) for:

1. **Aggregate-based types** — concrete classes inheriting `Aggregate<TState>`. For each, it collects the aggregate name, state type, public instance methods, and handled event types.
2. **Standalone state types** — concrete `State<T>` subclasses (classes or records) that are _not_ associated with any discovered aggregate. This enables support for the functional/aggregate-less `CommandService<TState>` pattern.

It emits a `[ModuleInitializer]` that registers all discovered types into the static `SpyglassRegistry` at application startup, with no DI registration required.

## Generated code

For each discovered type the generator emits a `SpyglassRegistry.Register(...)` call containing:

- **Aggregate name** (or `null` for standalone states)
- **State type name**
- **Public methods** (aggregate commands; empty for standalone states)
- **Handled event types** — resolved by instantiating the state and calling `GetRegisteredEventTypes()`
- **Load delegate** — a lambda that reads events from the store and rehydrates state:
  - For aggregates: creates the aggregate, calls `aggregate.Load(...)`
  - For standalone states: folds events with `state.When(e)`

### Example: aggregate

```csharp
// Discovered from: public class Booking : Aggregate<BookingState>
SpyglassRegistry.Register(new SpyglassAggregateInfo(
    "Booking",
    "BookingState",
    new string[] { "BookRoom", "RecordPayment" },
    new BookingState().GetRegisteredEventTypes().Select(t => t.Name).ToArray(),
    static async (eventStore, streamName, version) => {
        var aggregate = new Booking();
        var events = await eventStore.ReadStream(...);
        aggregate.Load(...);
        return new SpyglassLoadResult(aggregate.State, ...);
    }
));
```

### Example: standalone state (functional service)

```csharp
// Discovered from: public record PaymentState : State<PaymentState>
// (no Payment aggregate class exists)
SpyglassRegistry.Register(new SpyglassAggregateInfo(
    null,
    "PaymentState",
    System.Array.Empty<string>(),
    new PaymentState().GetRegisteredEventTypes().Select(t => t.Name).ToArray(),
    static async (eventStore, streamName, version) => {
        var events = await eventStore.ReadStream(...);
        var state = selected.Aggregate(new PaymentState(), (s, e) => s.When(e));
        return new SpyglassLoadResult(state, ...);
    }
));
```

## How to use

Reference the `Eventuous.Spyglass` package (or project). The generator is bundled with it automatically.

When using project references directly (not NuGet), add both:

```xml
<ProjectReference Include="...\Eventuous.Spyglass\Eventuous.Spyglass.csproj"/>
<ProjectReference Include="...\Eventuous.Spyglass.Generators\Eventuous.Spyglass.Generators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

Then map the Spyglass API endpoints in your app:

```csharp
app.MapEventuousSpyglass();
```

To inspect the generated source, add to your `.csproj`:

```xml
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
<CompilerGeneratedFilesOutputPath>obj/Generated</CompilerGeneratedFilesOutputPath>
```

The generated file will appear at `obj/Generated/.../SpyglassModule_{AssemblyName}.g.cs`.
