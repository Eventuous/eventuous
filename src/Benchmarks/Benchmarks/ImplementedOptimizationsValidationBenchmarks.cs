using BenchmarkDotNet.Attributes;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

/// <summary>
/// Validates the performance improvements from implemented optimizations.
/// Compares OLD implementations (before changes) with NEW implementations (after changes).
/// Reference: PERFORMANCE_ANALYSIS.md - Implemented P0/P1 optimizations
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ImplementedOptimizationsValidationBenchmarks {

    // ===== Issue #3: HandlingResults Optimization =====

    [Benchmark(Baseline = true, Description = "OLD: HandlingResults ConcurrentBag (single)")]
    public OldHandlingResults OldHandlingResults_Single() {
        var results = new OldHandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        _ = results.GetException();
        return results;
    }

    [Benchmark(Description = "NEW: HandlingResults Optimized (single)")]
    public HandlingResults NewHandlingResults_Single() {
        var results = new HandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        _ = results.GetException();
        return results;
    }

    [Benchmark(Description = "OLD: HandlingResults ConcurrentBag (3 results)")]
    public OldHandlingResults OldHandlingResults_Multiple() {
        var results = new OldHandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        results.Add(EventHandlingResult.Succeeded("Handler2"));
        results.Add(EventHandlingResult.Succeeded("Handler3"));
        _ = results.GetException();
        return results;
    }

    [Benchmark(Description = "NEW: HandlingResults Optimized (3 results)")]
    public HandlingResults NewHandlingResults_Multiple() {
        var results = new HandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        results.Add(EventHandlingResult.Succeeded("Handler2"));
        results.Add(EventHandlingResult.Succeeded("Handler3"));
        _ = results.GetException();
        return results;
    }

    // ===== Issue #2: ContextItems Lazy Initialization =====

    [Benchmark(Description = "OLD: ContextItems eager Dictionary (not used)")]
    public OldContextItems OldContextItems_NotUsed() {
        var items = new OldContextItems();
        return items;
    }

    [Benchmark(Description = "NEW: ContextItems lazy Dictionary (not used)")]
    public ContextItems NewContextItems_NotUsed() {
        var items = new ContextItems();
        return items;
    }

    [Benchmark(Description = "OLD: ContextItems eager Dictionary (used)")]
    public OldContextItems OldContextItems_Used() {
        var items = new OldContextItems();
        items.AddItem("key1", "value1");
        items.AddItem("key2", 42);
        _ = items.GetItem<string>("key1");
        return items;
    }

    [Benchmark(Description = "NEW: ContextItems lazy Dictionary (used)")]
    public ContextItems NewContextItems_Used() {
        var items = new ContextItems();
        items.AddItem("key1", "value1");
        items.AddItem("key2", 42);
        _ = items.GetItem<string>("key1");
        return items;
    }

    // ===== Issue #1: Logging Scope Dictionary =====

    private static readonly ILogger TestLogger = NullLogger.Instance;

    [Benchmark(Description = "OLD: Logging scope with Dictionary")]
    public IDisposable OldLoggingScope() {
        var scope = new Dictionary<string, object> {
            { "SubscriptionId", "TestSub" },
            { "Stream", "TestStream" },
            { "MessageType", "TestMessage" }
        };
        return TestLogger.BeginScope(scope)!;
    }

    [Benchmark(Description = "NEW: Logging scope with KeyValuePair array")]
    public IDisposable NewLoggingScope() {
        var scope = new KeyValuePair<string, object>[] {
            new("SubscriptionId", "TestSub"),
            new("Stream", "TestStream"),
            new("MessageType", "TestMessage")
        };
        return TestLogger.BeginScope(scope)!;
    }

    // ===== Issue #6: CancellationTokenSource Guards =====

    private static readonly CancellationToken SampleToken1 = new CancellationToken(false);
    private static readonly CancellationToken SampleToken2 = new CancellationToken(false);

    [Benchmark(Description = "OLD: Always create linked CTS")]
    public void OldCancellationTokenSource() {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(SampleToken1, SampleToken2);
        _ = cts.Token;
    }

    [Benchmark(Description = "NEW: Guarded CTS creation (same tokens)")]
    public void NewCancellationTokenSource_SameTokens() {
        CancellationToken token;
        if (SampleToken1 == SampleToken2) {
            token = SampleToken1;
        }
        else {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(SampleToken1, SampleToken2);
            token = cts.Token;
        }
        _ = token;
    }

    [Benchmark(Description = "NEW: Guarded CTS creation (non-cancelable)")]
    public void NewCancellationTokenSource_NonCancelable() {
        CancellationTokenSource? cts = null;
        CancellationToken token = SampleToken1;

        if (SampleToken1 == SampleToken2) {
            // Same token, no need to link
        }
        else if (!SampleToken2.CanBeCanceled) {
            // Worker token cannot be canceled, use context token as-is
        }
        else if (!SampleToken1.CanBeCanceled) {
            // Context token cannot be canceled, use worker token
            token = SampleToken2;
        }
        else {
            // Both can be canceled and are different - create linked token source
            cts = CancellationTokenSource.CreateLinkedTokenSource(SampleToken1, SampleToken2);
            token = cts.Token;
        }

        cts?.Dispose();
        _ = token;
    }

    // ===== OLD IMPLEMENTATIONS (Before Optimizations) =====

    /// <summary>
    /// OLD HandlingResults using ConcurrentBag (before optimization)
    /// </summary>
    public class OldHandlingResults {
        readonly ConcurrentBag<EventHandlingResult> _results = [];
        EventHandlingStatus _handlingStatus = 0;

        public void Add(EventHandlingResult result) {
            if (_results.Any(x => x.HandlerType == result.HandlerType)) return;
            _handlingStatus |= result.Status;
            _results.Add(result);
        }

        public IEnumerable<EventHandlingResult> GetResultsOf(EventHandlingStatus status)
            => _results.Where(x => x.Status == status);

        public EventHandlingStatus GetFailureStatus() => _handlingStatus & EventHandlingStatus.Handled;

        public EventHandlingStatus GetIgnoreStatus() => _handlingStatus & EventHandlingStatus.Ignored;

        public bool IsPending() => _handlingStatus == 0;

        public Exception? GetException() => _results.FirstOrDefault(x => x.Exception != null).Exception;
    }

    /// <summary>
    /// OLD ContextItems with eager Dictionary (before optimization)
    /// </summary>
    public class OldContextItems {
        readonly Dictionary<string, object?> _items = new();

        public OldContextItems AddItem(string key, object? value) {
            _items.TryAdd(key, value);
            return this;
        }

        public T? GetItem<T>(string key)
            => _items.TryGetValue(key, out var value) && value is T val ? val : default;

        public bool TryGetItem<T>(string key, out T? value) {
            if (_items.TryGetValue(key, out var val) && val is T val2) {
                value = val2;
                return true;
            }
            value = default;
            return false;
        }
    }
}
