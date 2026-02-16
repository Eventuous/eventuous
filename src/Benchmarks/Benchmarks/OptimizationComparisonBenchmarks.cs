using BenchmarkDotNet.Attributes;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;

namespace Benchmarks;

/// <summary>
/// Benchmarks comparing current implementation vs optimized alternatives.
/// Demonstrates the potential improvements from P0 optimizations.
/// Reference: PERFORMANCE_ANALYSIS.md - P0 priority items
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class OptimizationComparisonBenchmarks {
    [Benchmark(Baseline = true, Description = "Current: ContextItems with Dictionary")]
    public ContextItems CurrentContextItems() {
        var items = new ContextItems();
        items.AddItem("key1", "value1");
        items.AddItem("key2", 42);
        var val = items.GetItem<string>("key1");
        return items;
    }

    [Benchmark(Description = "Optimized: Lazy ContextItems")]
    public OptimizedContextItems OptimizedLazyContextItems() {
        var items = new OptimizedContextItems();
        items.AddItem("key1", "value1");
        items.AddItem("key2", 42);
        var val = items.GetItem<string>("key1");
        return items;
    }

    [Benchmark(Description = "Current: HandlingResults with ConcurrentBag")]
    public HandlingResults CurrentHandlingResults() {
        var results = new HandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        _ = results.GetFailureStatus();
        return results;
    }

    [Benchmark(Description = "Optimized: HandlingResults single result")]
    public OptimizedHandlingResults OptimizedSingleResult() {
        var results = new OptimizedHandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        _ = results.GetFailureStatus();
        return results;
    }

    [Benchmark(Description = "Current: HandlingResults 3 results")]
    public HandlingResults CurrentMultipleResults() {
        var results = new HandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        results.Add(EventHandlingResult.Succeeded("Handler2"));
        results.Add(EventHandlingResult.Succeeded("Handler3"));
        _ = results.GetFailureStatus();
        return results;
    }

    [Benchmark(Description = "Optimized: HandlingResults 3 results")]
    public OptimizedHandlingResults OptimizedMultipleResults() {
        var results = new OptimizedHandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        results.Add(EventHandlingResult.Succeeded("Handler2"));
        results.Add(EventHandlingResult.Succeeded("Handler3"));
        _ = results.GetFailureStatus();
        return results;
    }

    /// <summary>
    /// Optimized ContextItems with lazy dictionary initialization
    /// </summary>
    public class OptimizedContextItems {
        Dictionary<string, object?>? _items;

        public OptimizedContextItems AddItem(string key, object? value) {
            _items ??= new();
            _items.TryAdd(key, value);
            return this;
        }

        public T? GetItem<T>(string key) {
            if (_items == null) return default;
            return _items.TryGetValue(key, out var value) && value is T val ? val : default;
        }

        public bool TryGetItem<T>(string key, out T? value) {
            if (_items != null && _items.TryGetValue(key, out var val) && val is T val2) {
                value = val2;
                return true;
            }
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Optimized HandlingResults that uses single field for common case
    /// </summary>
    public class OptimizedHandlingResults {
        EventHandlingResult? _singleResult;
        List<EventHandlingResult>? _multipleResults;
        EventHandlingStatus _handlingStatus;

        public void Add(EventHandlingResult result) {
            // Single result case (most common)
            if (_singleResult == null && _multipleResults == null) {
                _singleResult = result;
                _handlingStatus = result.Status;
                return;
            }

            // Transition to multiple results
            if (_multipleResults == null && _singleResult != null) {
                _multipleResults = [_singleResult.Value];
                _singleResult    = null;
            }

            // Check for duplicate handler
            if (_multipleResults != null) {
                for (int i = 0; i < _multipleResults.Count; i++) {
                    if (_multipleResults[i].HandlerType == result.HandlerType) return;
                }
                _handlingStatus |= result.Status;
                _multipleResults.Add(result);
            }
        }

        public IEnumerable<EventHandlingResult> GetResultsOf(EventHandlingStatus status) {
            if (_singleResult != null && _singleResult.Value.Status == status) {
                yield return _singleResult.Value;
            }
            else if (_multipleResults != null) {
                for (int i = 0; i < _multipleResults.Count; i++) {
                    if (_multipleResults[i].Status == status) {
                        yield return _multipleResults[i];
                    }
                }
            }
        }

        public EventHandlingStatus GetFailureStatus() => _handlingStatus & EventHandlingStatus.Handled;

        public EventHandlingStatus GetIgnoreStatus() => _handlingStatus & EventHandlingStatus.Ignored;

        public bool IsPending() => _handlingStatus == 0;

        public Exception? GetException() {
            if (_singleResult?.Exception != null) return _singleResult.Value.Exception;
            if (_multipleResults != null) {
                for (int i = 0; i < _multipleResults.Count; i++) {
                    if (_multipleResults[i].Exception != null) return _multipleResults[i].Exception;
                }
            }
            return null;
        }
    }
}
