using BenchmarkDotNet.Attributes;
using System.Text;

namespace Benchmarks;

/// <summary>
/// Benchmarks for common allocation hotspots identified in the analysis.
/// Focuses on dictionary allocations, string formatting, and LINQ usage.
/// Reference: PERFORMANCE_ANALYSIS.md sections 1, 13
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class AllocationHotspotsBenchmarks {
    string _subscriptionId = null!;
    string _streamName = null!;
    string _messageType = null!;

    [GlobalSetup]
    public void Setup() {
        _subscriptionId = "test-subscription-id";
        _streamName = "test-stream-name";
        _messageType = "TestEventType";
    }

    [Benchmark(Baseline = true, Description = "Dictionary for logging scope (current)")]
    public Dictionary<string, object> CreateLoggingScopeDictionary() {
        return new() {
            { "SubscriptionId", _subscriptionId },
            { "Stream", _streamName },
            { "MessageType", _messageType }
        };
    }

    [Benchmark(Description = "Array of KeyValuePairs (alternative)")]
    public KeyValuePair<string, object>[] CreateLoggingScopeArray() {
        return [
            new("SubscriptionId", _subscriptionId),
            new("Stream", _streamName),
            new("MessageType", _messageType)
        ];
    }

    [Benchmark(Description = "Activity name - string interpolation")]
    public string ActivityNameInterpolation() {
        return $"Subscription.{_subscriptionId}/{_messageType}";
    }

    [Benchmark(Description = "Activity name - string concat")]
    public string ActivityNameConcat() {
        return string.Concat("Subscription.", _subscriptionId, "/", _messageType);
    }

    [Benchmark(Description = "Activity name - StringBuilder")]
    public string ActivityNameStringBuilder() {
        var sb = new StringBuilder(64);
        sb.Append("Subscription.");
        sb.Append(_subscriptionId);
        sb.Append('/');
        sb.Append(_messageType);
        return sb.ToString();
    }

    [Benchmark(Description = "LINQ Any() check on small list")]
    public bool LinqAnyOnSmallList() {
        var list = new List<string> { "item1", "item2", "item3" };
        return list.Any(x => x == "item2");
    }

    [Benchmark(Description = "Manual iteration on small list")]
    public bool ManualIterationOnSmallList() {
        var list = new List<string> { "item1", "item2", "item3" };
        foreach (var item in list) {
            if (item == "item2") return true;
        }
        return false;
    }

    [Benchmark(Description = "LINQ Where().Any() pattern")]
    public bool LinqWhereAny() {
        var items = Enumerable.Range(0, 20).Select(i => new TestItem { Id = i, Active = i % 2 == 0 });
        return items.Any(x => x.Active);
    }

    [Benchmark(Description = "LINQ Any() with predicate")]
    public bool LinqAnyWithPredicate() {
        var items = Enumerable.Range(0, 20).Select(i => new TestItem { Id = i, Active = i % 2 == 0 });
        return items.Any(x => x.Active);
    }

    [Benchmark(Description = "Manual enumeration check")]
    public bool ManualEnumerationCheck() {
        var items = Enumerable.Range(0, 20).Select(i => new TestItem { Id = i, Active = i % 2 == 0 });
        foreach (var item in items) {
            if (item.Active) return true;
        }
        return false;
    }

    [Benchmark(Description = "CancellationTokenSource creation")]
    public CancellationTokenSource CreateCancellationTokenSource() {
        var cts = new CancellationTokenSource();
        cts.Dispose();
        return cts;
    }

    [Benchmark(Description = "Linked CancellationTokenSource")]
    public CancellationTokenSource CreateLinkedCancellationTokenSource() {
        var cts1 = new CancellationTokenSource();
        var cts2 = new CancellationTokenSource();
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);
        linked.Dispose();
        cts2.Dispose();
        cts1.Dispose();
        return linked;
    }

    [Benchmark(Description = "Guid.ToString()")]
    public string GuidToString() {
        return Guid.NewGuid().ToString();
    }

    [Benchmark(Description = "DateTime.UtcNow allocation")]
    public DateTime GetUtcNow() {
        return DateTime.UtcNow;
    }

    class TestItem {
        public int Id { get; set; }
        public bool Active { get; set; }
    }
}
