using BenchmarkDotNet.Attributes;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;

namespace Benchmarks;

/// <summary>
/// Benchmarks focusing on context allocation overhead.
/// Addresses P0 issues: ContextItems, HandlingResults, MessageConsumeContext wrappers.
/// Reference: PERFORMANCE_ANALYSIS.md sections 2, 3, 4
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ContextAllocationBenchmarks {
    MessageConsumeContext _baseContext = null!;

    [GlobalSetup]
    public void Setup() {
        _baseContext = new(
            eventId: Guid.NewGuid().ToString(),
            eventType: "TestEvent",
            contentType: "application/json",
            stream: "test-stream",
            eventNumber: 1,
            streamPosition: 1,
            globalPosition: 1,
            sequence: 1,
            created: DateTime.UtcNow,
            message: new TestMessage { Value = "test" },
            metadata: null,
            subscriptionId: "test-subscription",
            cancellationToken: CancellationToken.None
        );
    }

    [Benchmark(Baseline = true, Description = "Context creation (baseline)")]
    public MessageConsumeContext CreateContext() {
        return new(
            eventId: Guid.NewGuid().ToString(),
            eventType: "TestEvent",
            contentType: "application/json",
            stream: "test-stream",
            eventNumber: 1,
            streamPosition: 1,
            globalPosition: 1,
            sequence: 1,
            created: DateTime.UtcNow,
            message: new TestMessage { Value = "test" },
            metadata: null,
            subscriptionId: "test-subscription",
            cancellationToken: CancellationToken.None
        );
    }

    [Benchmark(Description = "Context + ContextItems usage")]
    public MessageConsumeContext CreateContextWithItems() {
        var ctx = new MessageConsumeContext(
            eventId: Guid.NewGuid().ToString(),
            eventType: "TestEvent",
            contentType: "application/json",
            stream: "test-stream",
            eventNumber: 1,
            streamPosition: 1,
            globalPosition: 1,
            sequence: 1,
            created: DateTime.UtcNow,
            message: new TestMessage { Value = "test" },
            metadata: null,
            subscriptionId: "test-subscription",
            cancellationToken: CancellationToken.None
        );

        // Simulate adding items (common in filters)
        ctx.Items.AddItem("key1", "value1");
        ctx.Items.AddItem("key2", 42);

        return ctx;
    }

    [Benchmark(Description = "Typed context wrapper creation")]
    public MessageConsumeContext<TestMessage> CreateTypedContextWrapper() {
        return new(_baseContext);
    }

    [Benchmark(Description = "HandlingResults - single result")]
    public HandlingResults SingleHandlerResult() {
        var results = new HandlingResults();
        results.Add(EventHandlingResult.Succeeded("TestHandler"));
        return results;
    }

    [Benchmark(Description = "HandlingResults - multiple results")]
    public HandlingResults MultipleHandlerResults() {
        var results = new HandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        results.Add(EventHandlingResult.Succeeded("Handler2"));
        results.Add(EventHandlingResult.Succeeded("Handler3"));
        return results;
    }

    [Benchmark(Description = "HandlingResults with failure check")]
    public bool HandlingResultsWithCheck() {
        var results = new HandlingResults();
        results.Add(EventHandlingResult.Succeeded("Handler1"));
        results.Add(EventHandlingResult.Failed("Handler2", new("test")));
        results.Add(EventHandlingResult.Ignored("Handler3"));

        return results.GetFailureStatus() == EventHandlingStatus.Failure;
    }

    [Benchmark(Description = "ContextItems - no usage (empty)")]
    public ContextItems EmptyContextItems() {
        return new ContextItems();
    }

    [Benchmark(Description = "ContextItems - add and retrieve")]
    public object? ContextItemsUsage() {
        var items = new ContextItems();
        items.AddItem("key1", "value1");
        items.AddItem("key2", 42);
        items.AddItem("key3", DateTime.UtcNow);

        return items.GetItem<string>("key1");
    }

    [Benchmark(Description = "Full message processing simulation")]
    public bool FullMessageProcessingSimulation() {
        // Create context
        var ctx = new MessageConsumeContext(
            eventId: Guid.NewGuid().ToString(),
            eventType: "TestEvent",
            contentType: "application/json",
            stream: "test-stream",
            eventNumber: 1,
            streamPosition: 1,
            globalPosition: 1,
            sequence: 1,
            created: DateTime.UtcNow,
            message: new TestMessage { Value = "test" },
            metadata: null,
            subscriptionId: "test-subscription",
            cancellationToken: CancellationToken.None
        );

        // Add some context items (filter scenario)
        ctx.Items.AddItem("partition", 5);

        // Create typed wrapper (handler scenario)
        var typedCtx = new MessageConsumeContext<TestMessage>(ctx);

        // Add handling results
        ctx.HandlingResults.Add(EventHandlingResult.Succeeded("TestHandler"));

        // Check results
        return !ctx.HandlingResults.IsPending();
    }

    public class TestMessage {
        public string Value { get; set; } = string.Empty;
    }
}
