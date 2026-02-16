using BenchmarkDotNet.Attributes;
using System.Buffers;

namespace Benchmarks;

/// <summary>
/// Benchmarks for channel batching ToArray optimization.
/// Tests different approaches to returning batched results.
/// Reference: PERFORMANCE_ANALYSIS.md - Issue #10: Channel Batching List.ToArray()
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ChannelBatchingBenchmarks {
    private List<int> _buffer = null!;

    [Params(10, 50, 100)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup() {
        _buffer = new List<int>(BatchSize);
        for (int i = 0; i < BatchSize; i++) {
            _buffer.Add(i);
        }
    }

    [Benchmark(Baseline = true, Description = "Current: List.ToArray()")]
    public int[] CurrentApproach_ToArray() {
        return _buffer.ToArray();
    }

    [Benchmark(Description = "Alternative 1: CollectionsMarshal.AsSpan()")]
    public ReadOnlySpan<int> Alternative1_CollectionsMarshalAsSpan() {
        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer);
    }

    [Benchmark(Description = "Alternative 2: ArrayPool rent/copy")]
    public int[] Alternative2_ArrayPool() {
        var array = ArrayPool<int>.Shared.Rent(_buffer.Count);
        _buffer.CopyTo(array);
        return array; // Note: caller must return to pool
    }

    [Benchmark(Description = "Alternative 3: Pre-allocated array with CopyTo")]
    public int[] Alternative3_PreAllocatedArray() {
        var array = new int[_buffer.Count];
        _buffer.CopyTo(array);
        return array;
    }

    [Benchmark(Description = "Alternative 4: Direct List (no copy)")]
    public List<int> Alternative4_DirectList() {
        return _buffer; // Returns list directly - consumer must handle as read-only
    }

    [Benchmark(Description = "Alternative 5: IReadOnlyList wrapper")]
    public IReadOnlyList<int> Alternative5_ReadOnlyWrapper() {
        return _buffer.AsReadOnly();
    }

    // Cleanup benchmark (shows ArrayPool return overhead)
    private int[]? _rentedArray;

    [IterationSetup(Target = nameof(WithArrayPoolReturnOverhead))]
    public void SetupRentedArray() {
        _rentedArray = ArrayPool<int>.Shared.Rent(_buffer.Count);
        _buffer.CopyTo(_rentedArray);
    }

    [Benchmark(Description = "Alternative 2b: ArrayPool with return overhead")]
    public int[] WithArrayPoolReturnOverhead() {
        var result = _rentedArray;
        return result!;
    }

    [IterationCleanup(Target = nameof(WithArrayPoolReturnOverhead))]
    public void CleanupRentedArray() {
        if (_rentedArray != null) {
            ArrayPool<int>.Shared.Return(_rentedArray);
            _rentedArray = null;
        }
    }
}
