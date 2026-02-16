# Eventuous Subscriptions Benchmarks

This directory contains performance benchmarks for the Eventuous.Subscriptions project, created to validate the optimizations suggested in [PERFORMANCE_ANALYSIS.md](../../Core/src/Eventuous.Subscriptions/PERFORMANCE_ANALYSIS.md).

## Benchmark Files

### 1. SubscriptionMessageProcessingBenchmarks.cs
**Purpose**: Measures end-to-end message processing throughput and latency.

**What it tests**:
- Single message handler invocation
- Batch message processing (1, 10, 100 messages)
- Context creation + processing overhead

**Key Metrics**: Operations/sec, memory allocations per operation

**Validates**: Overall system throughput characteristics

**Note**: Parameters limited to 1, 10, 100 to keep benchmark runtime reasonable

---

### 2. ContextAllocationBenchmarks.cs
**Purpose**: Focuses on allocation overhead in context creation and management.

**What it tests**:
- MessageConsumeContext creation (baseline)
- ContextItems usage patterns (P0 issue #2)
- HandlingResults with single/multiple handlers (P0 issue #3)
- MessageConsumeContext<T> wrapper creation (P0 issue #4)
- Full message processing simulation

**Key Metrics**: Allocated bytes per operation, Gen0 collections

**Validates**: P0 optimizations from performance analysis sections 2, 3, 4

---

### 3. AllocationHotspotsBenchmarks.cs
**Purpose**: Compares allocation patterns for common operations.

**What it tests**:
- Logging scope dictionary creation (P0 issue #1)
- Activity name string formatting (P2 issue #13)
- LINQ vs manual iteration patterns
- CancellationTokenSource allocations (P1 issue #6)
- Common allocations (Guid, DateTime)

**Key Metrics**: Allocated bytes, allocation rate comparison

**Validates**: Multiple P0/P1/P2 issues related to allocations

---

### 4. OptimizationComparisonBenchmarks.cs
**Purpose**: Direct before/after comparison of proposed optimizations.

**What it tests**:
- Current ContextItems vs lazy-initialized version
- Current HandlingResults (ConcurrentBag) vs optimized (single field)
- Single handler vs multiple handlers scenarios

**Key Metrics**: Side-by-side allocation and performance comparison

**Validates**: Actual impact of proposed P0 optimizations

---

## Disabled Benchmarks

The following benchmarks are currently disabled due to excessive runtime with async operations:

### CheckpointBenchmarks.cs (DISABLED)
- **Issue**: Async checkpoint commits with channels take 2-3 hours to complete in BenchmarkDotNet
- **Reason**: Benchmark measurement overhead makes async channel operations impractical to measure
- **Alternative**: Profile checkpoint performance in actual application scenarios or integration tests
- **To re-enable**: Remove `CheckpointBenchmarks.cs` from `<Compile Remove>` in Benchmarks.csproj

### FilterPipelineBenchmarks.cs (DISABLED)
- **Issue**: AsyncHandlingFilter with channels takes 3-4 hours even with minimal parameters (1, 10 messages)
- **Reason**: Async channel operations are incompatible with BenchmarkDotNet's measurement approach
- **Alternative**: Measure filter pipeline performance through integration tests or application telemetry
- **To re-enable**: Remove `FilterPipelineBenchmarks.cs` from `<Compile Remove>` in Benchmarks.csproj

**Note**: These operations are fast in real-world usage. The slowness is specific to the benchmark measurement context.

---

## Running the Benchmarks

### Run all benchmarks:
```bash
cd src/Benchmarks/Benchmarks
dotnet run -c Release
```

### Run specific benchmark class:
```bash
dotnet run -c Release --filter "*ContextAllocationBenchmarks*"
```

### Run specific benchmark method:
```bash
dotnet run -c Release --filter "*ContextAllocationBenchmarks.CreateContext*"
```

### Run with specific parameters:
```bash
dotnet run -c Release --filter "*SubscriptionMessageProcessingBenchmarks*" --job short
```

## Understanding Results

### Key Metrics to Watch

1. **Mean (μ)**: Average execution time
2. **Error**: Measurement error
3. **StdDev**: Standard deviation
4. **Allocated**: Total bytes allocated per operation
5. **Gen0/Gen1/Gen2**: Garbage collection counts

### What Good Results Look Like

- **Low allocations**: < 1 KB per message for hot path operations
- **No Gen1/Gen2 collections**: Only Gen0 for high-frequency operations
- **Consistent timings**: Low StdDev indicates predictable performance
- **Linear scaling**: Batch operations should scale linearly with count

### Baseline Comparison

Many benchmarks include `[Benchmark(Baseline = true)]` to establish a reference point. Results will show:
- **Ratio**: How much slower/faster compared to baseline
- **RatioSD**: Standard deviation of the ratio

Example:
```
| Method    | Mean    | Allocated | Ratio |
|---------- |--------:|----------:|------:|
| Current   | 100 ns  | 120 B     | 1.00  |  ← Baseline
| Optimized |  50 ns  |  40 B     | 0.50  |  ← 2x faster, 3x less allocation
```

## Interpreting Results for Optimization Decisions

### Priority 0 (Critical) Items

**ContextItems Dictionary** (`ContextAllocationBenchmarks`):
- Look for allocation difference between `CreateContext` and `CreateContextWithItems`
- Target: Lazy initialization should reduce allocations when items not used

**HandlingResults** (`OptimizationComparisonBenchmarks`):
- Compare `CurrentHandlingResults` vs `OptimizedSingleResult`
- Target: 50%+ allocation reduction for single handler case

**Logging Scope Dictionary** (`AllocationHotspotsBenchmarks`):
- Compare dictionary creation methods
- Target: Alternatives should show reduced allocations

### Expected Improvements (P0 + P1)

After implementing suggested optimizations:
- **30-50% reduction** in total allocations per message
- **20-40% improvement** in throughput
- Fewer Gen0 collections per 1000 operations
- More consistent latency (lower StdDev)

## Continuous Benchmarking

### Establish Baselines
```bash
# Run and save baseline results
dotnet run -c Release --exporters json > baseline-results.json
```

### Compare After Changes
```bash
# Run again and compare
dotnet run -c Release --exporters json > optimized-results.json

# Use BenchmarkDotNet comparison tools
dotnet run -c Release --filter "*" --join --baseline baseline-results.json
```

## Benchmark Parameters and Runtime

### Parameter Choices

The benchmark parameters have been tuned for reasonable runtime while still providing meaningful results:

**Active Benchmarks:**
- **SubscriptionMessageProcessingBenchmarks**: 1, 10, 100 messages
- **ContextAllocationBenchmarks**: No parameters (single operations)
- **AllocationHotspotsBenchmarks**: No parameters (single operations)
- **OptimizationComparisonBenchmarks**: No parameters (single operations)

**Disabled (too slow):**
- ~~CheckpointBenchmarks~~ - Async operations incompatible with BenchmarkDotNet
- ~~FilterPipelineBenchmarks~~ - Async channel operations take hours

### Expected Runtime

Running all **active** benchmarks with default settings:
- **Quick benchmarks** (AllocationHotspots, ContextAllocation, Optimization): ~5-10 minutes
- **Medium benchmarks** (SubscriptionMessageProcessing): ~10-15 minutes
- **Total estimated time**: ~15-25 minutes for full suite

To reduce runtime:
```bash
# Run only fast benchmarks
dotnet run -c Release --filter "*AllocationHotspotsBenchmarks*"
dotnet run -c Release --filter "*ContextAllocationBenchmarks*"

# Use shorter job configuration
dotnet run -c Release --job short
```

## Benchmark Maintenance

### Adding New Benchmarks

When adding new benchmarks:
1. Follow the existing pattern (MemoryDiagnoser, SimpleJob)
2. Include clear descriptions via `[Benchmark(Description = "...")]`
3. Use `[Params]` for parameterized tests - **keep ranges small** (max 3-4 values)
4. Add proper setup/cleanup with `[GlobalSetup]`/`[GlobalCleanup]`
5. Test runtime before committing - benchmarks shouldn't take more than 5 minutes each
6. Document in this README

### When to Run

- **Before optimization work**: Establish baseline
- **After each P0 optimization**: Validate improvement
- **Before releases**: Ensure no regressions
- **After dependency updates**: Check for performance changes

## Related Documentation

- [PERFORMANCE_ANALYSIS.md](../../Core/src/Eventuous.Subscriptions/PERFORMANCE_ANALYSIS.md) - Detailed analysis and recommendations
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/articles/overview.html) - Tool usage guide

## Troubleshooting

### Benchmark taking too long
```bash
# Use shorter job
dotnet run -c Release --job short
```

### Need more detailed results
```bash
# Add memory diagnoser and disassembly
dotnet run -c Release --disasm
```

### Results too noisy
```bash
# Increase warmup and iteration counts
dotnet run -c Release --warmupCount 5 --iterationCount 10
```

## Notes

- Always run benchmarks in **Release** configuration
- Close other applications to reduce noise
- Run multiple times to verify consistency
- Results may vary by hardware - document your environment
- Focus on relative improvements, not absolute numbers
