using BenchmarkDotNet.Attributes;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

/// <summary>
/// Benchmarks for checkpoint commit operations.
/// Addresses P2 issue: CommitPositionSequence LINQ usage and gap detection.
/// Reference: PERFORMANCE_ANALYSIS.md section 11
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class CheckpointBenchmarks {
    NoOpCheckpointStore _store = null!;
    CheckpointCommitHandler _handler = null!;
    LogContext _logContext = null!;
    CommitPosition[] _sequentialPositions = null!;
    CommitPosition[] _positionsWithGaps = null!;

    [Params(10, 100)]
    public int PositionCount { get; set; }

    [GlobalSetup]
    public void Setup() {
        _store = new();
        _handler = new(
            "test-subscription",
            _store,
            TimeSpan.FromMilliseconds(100),
            batchSize: 10
        );

        _logContext = new("test", new NullLoggerFactory());

        // Sequential positions (no gaps)
        _sequentialPositions = new CommitPosition[PositionCount];
        for (int i = 0; i < PositionCount; i++) {
            _sequentialPositions[i] = new(
                (ulong)i,
                (ulong)i,
                DateTime.UtcNow
            ) { LogContext = _logContext };
        }

        // Positions with gaps (every 10th position missing)
        var positionsWithGaps = new List<CommitPosition>();
        for (int i = 0; i < PositionCount; i++) {
            if (i % 10 != 0) {
                positionsWithGaps.Add(new(
                    (ulong)i,
                    (ulong)i,
                    DateTime.UtcNow
                ) { LogContext = _logContext });
            }
        }
        _positionsWithGaps = positionsWithGaps.ToArray();
    }

    [IterationSetup]
    public void IterationSetup() {
        _handler = new(
            "test-subscription",
            _store,
            TimeSpan.FromMilliseconds(100),
            batchSize: 10
        );
    }

    [IterationCleanup]
    public void IterationCleanup() {
        _handler.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Description = "Sequential checkpoint commits")]
    public async Task CommitSequentialCheckpoints() {
        foreach (var position in _sequentialPositions) {
            await _handler.Commit(position, CancellationToken.None);
        }
    }

    [Benchmark(Description = "Checkpoint commits with gaps")]
    public async Task CommitCheckpointsWithGaps() {
        foreach (var position in _positionsWithGaps) {
            await _handler.Commit(position, CancellationToken.None);
        }
    }

    [Benchmark(Description = "CommitPositionSequence - add sequential")]
    public CommitPositionSequence BuildSequentialSequence() {
        var sequence = new CommitPositionSequence();
        for (int i = 0; i < PositionCount; i++) {
            sequence.Add(new((ulong)i, (ulong)i, DateTime.UtcNow));
        }
        return sequence;
    }

    [Benchmark(Description = "CommitPositionSequence - add with gaps")]
    public CommitPositionSequence BuildSequenceWithGaps() {
        var sequence = new CommitPositionSequence();
        for (int i = 0; i < PositionCount; i++) {
            if (i % 10 != 0) {
                sequence.Add(new((ulong)i, (ulong)i, DateTime.UtcNow));
            }
        }
        return sequence;
    }

    [Benchmark(Description = "CommitPositionSequence - gap detection")]
    public CommitPosition DetectGaps() {
        var sequence = new CommitPositionSequence();
        for (int i = 0; i < PositionCount; i++) {
            if (i % 10 != 0) {
                sequence.Add(new((ulong)i, (ulong)i, DateTime.UtcNow));
            }
        }

        // This triggers the LINQ-based gap detection (FirstBeforeGap)
        return sequence.FirstBeforeGap();
    }

    [Benchmark(Description = "Checkpoint store operations")]
    public async Task<Checkpoint> CheckpointStoreRoundtrip() {
        var checkpoint = new Checkpoint("test-subscription", 12345);
        await _store.StoreCheckpoint(checkpoint, false, CancellationToken.None);
        return await _store.GetLastCheckpoint("test-subscription", CancellationToken.None);
    }
}
