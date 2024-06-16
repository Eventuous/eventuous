// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

[MemoryDiagnoser]
public class GapDetectionBenchmarks {
    int[]                   _numbers = null!;
    NoOpCheckpointStore     _store   = null!;
    CheckpointCommitHandler _cch     = null!;
    LogContext              _log     = null!;

    [GlobalSetup]
    public void Setup() {
        _store = new NoOpCheckpointStore();

        _store.CheckpointStored += (_, checkpoint) => Console.WriteLine(checkpoint);

        var numbers = Enumerable.Range(1, 1000).ToList();
        numbers.RemoveAll(x => x % 10 == 0);
        _numbers = numbers.ToArray();

        _log = new LogContext("test", new NullLoggerFactory());
    }

    [IterationSetup]
    public void IterationSetup() {
        _cch = new CheckpointCommitHandler("test", _store, TimeSpan.FromMilliseconds(1000), 10);
    }

    [IterationCleanup]
    public void IterationCleanup() {
        _cch.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public async Task CheckpointCommitHandler() {
        foreach (var number in _numbers) {
            var p = new CommitPosition((ulong)number, (ulong)number, DateTime.MinValue) { LogContext = _log };
            await _cch.Commit(p, CancellationToken.None);
        }
    }
}