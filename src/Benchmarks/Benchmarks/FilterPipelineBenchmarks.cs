using BenchmarkDotNet.Attributes;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Consumers;

namespace Benchmarks;

/// <summary>
/// Benchmarks for filter pipeline processing.
/// Measures overhead of filter chain, async handling, and partitioning.
/// Reference: PERFORMANCE_ANALYSIS.md sections on filtering and async processing
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class FilterPipelineBenchmarks {
    ConsumePipe _simplePipe = null!;
    ConsumePipe _asyncPipe = null!;
    ConsumePipe _multiFilterPipe = null!;
    MessageConsumeContext _context = null!;
    AsyncConsumeContext _asyncContext = null!;

    [Params(1, 10)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup() {
        // Simple pipe with just a consumer
        _simplePipe = new ConsumePipe();
        _simplePipe.AddFilterLast(new ConsumerFilter(new NoOpConsumer()));

        // Pipe with async handling filter
        _asyncPipe = new ConsumePipe();
        _asyncPipe.AddFilterFirst(new AsyncHandlingFilter(1)); // Single concurrency
        _asyncPipe.AddFilterLast(new ConsumerFilter(new NoOpConsumer()));

        // Pipe with multiple filters
        _multiFilterPipe = new();
        _multiFilterPipe.AddFilterFirst(new AsyncHandlingFilter(4)); // Concurrent
        _multiFilterPipe.AddFilterLast(new TracingFilter("test-consumer"));
        _multiFilterPipe.AddFilterLast(new ConsumerFilter(new NoOpConsumer()));

        // Create test contexts
        _context = new MessageConsumeContext(
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

        _asyncContext = new(
            _context,
            _ => ValueTask.CompletedTask,
            (_, _) => ValueTask.CompletedTask
        );
    }

    [GlobalCleanup]
    public async Task Cleanup() {
        await _simplePipe.DisposeAsync();
        await _asyncPipe.DisposeAsync();
        await _multiFilterPipe.DisposeAsync();
    }

    [Benchmark(Baseline = true, Description = "Simple pipe (consumer only)")]
    public async Task SimplePipeline() {
        await _simplePipe.Send(_context);
    }

    [Benchmark(Description = "Pipe with AsyncHandlingFilter")]
    public async Task AsyncPipeline() {
        await _asyncPipe.Send(_asyncContext);
    }

    [Benchmark(Description = "Multi-filter pipeline")]
    public async Task MultiFilterPipeline() {
        await _multiFilterPipe.Send(_asyncContext);
    }

    [Benchmark(Description = "Batch through simple pipe")]
    public async Task BatchThroughSimplePipe() {
        for (int i = 0; i < MessageCount; i++) {
            await _simplePipe.Send(_context);
        }
    }

    [Benchmark(Description = "Batch through async pipe")]
    public async Task BatchThroughAsyncPipe() {
        for (int i = 0; i < MessageCount; i++) {
            await _asyncPipe.Send(_asyncContext);
        }
    }

    [Benchmark(Description = "Create and send through pipe")]
    public async Task CreateContextAndSend() {
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

        await _simplePipe.Send(ctx);
    }

    class TestMessage {
        public string Value { get; set; } = string.Empty;
    }

    class NoOpConsumer : IMessageConsumer<IMessageConsumeContext> {
        public ValueTask Consume(IMessageConsumeContext context) {
            // Simulate minimal work
            _ = context.MessageType;
            context.Ack("NoOpConsumer");
            return ValueTask.CompletedTask;
        }
    }
}
