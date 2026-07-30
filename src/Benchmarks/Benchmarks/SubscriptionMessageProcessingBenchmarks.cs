using BenchmarkDotNet.Attributes;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;

namespace Benchmarks;

/// <summary>
/// Benchmarks for subscription message processing throughput and allocations.
/// Validates P0 optimizations from PERFORMANCE_ANALYSIS.md
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class SubscriptionMessageProcessingBenchmarks {
    TestEventHandler _handler = null!;
    MessageConsumeContext _context = null!;
    IMessageConsumeContext[] _contexts = null!;

    [Params(1, 10, 100)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup() {
        _handler = new();

        // Create a sample message context
        _context = new(
            eventId: Guid.NewGuid().ToString(),
            eventType: nameof(TestEvent),
            contentType: "application/json",
            stream: "test-stream",
            eventNumber: 1,
            streamPosition: 1,
            globalPosition: 1,
            sequence: 1,
            created: DateTime.UtcNow,
            message: new TestEvent { Value = "test" },
            metadata: null,
            subscriptionId: "test-subscription",
            cancellationToken: CancellationToken.None
        );

        // Pre-create contexts for batched processing
        _contexts = new IMessageConsumeContext[MessageCount];
        for (int i = 0; i < MessageCount; i++) {
            _contexts[i] = new MessageConsumeContext(
                eventId: Guid.NewGuid().ToString(),
                eventType: nameof(TestEvent),
                contentType: "application/json",
                stream: $"test-stream-{i}",
                eventNumber: (ulong)i,
                streamPosition: (ulong)i,
                globalPosition: (ulong)i,
                sequence: (ulong)i,
                created: DateTime.UtcNow,
                message: new TestEvent { Value = $"test-{i}" },
                metadata: null,
                subscriptionId: "test-subscription",
                cancellationToken: CancellationToken.None
            );
        }
    }

    [Benchmark(Description = "Single message handler invocation")]
    public async Task<EventHandlingStatus> ProcessSingleMessage() {
        return await _handler.HandleEvent(_context);
    }

    [Benchmark(Description = "Batch message processing")]
    public async Task ProcessMessageBatch() {
        for (int i = 0; i < MessageCount; i++) {
            await _handler.HandleEvent(_contexts[i]);
        }
    }

    [Benchmark(Description = "Context creation + processing")]
    public async Task CreateContextAndProcess() {
        var ctx = new MessageConsumeContext(
            eventId: Guid.NewGuid().ToString(),
            eventType: nameof(TestEvent),
            contentType: "application/json",
            stream: "test-stream",
            eventNumber: 1,
            streamPosition: 1,
            globalPosition: 1,
            sequence: 1,
            created: DateTime.UtcNow,
            message: new TestEvent { Value = "test" },
            metadata: null,
            subscriptionId: "test-subscription",
            cancellationToken: CancellationToken.None
        );

        await _handler.HandleEvent(ctx);
    }

    class TestEvent {
        public string Value { get; set; } = string.Empty;
    }

    class TestEventHandler : Eventuous.Subscriptions.EventHandler {
        public TestEventHandler() {
            On<TestEvent>(Handle);
        }

        static ValueTask Handle(MessageConsumeContext<TestEvent> context) {
            // Simulate minimal processing
            _ = context.Message.Value;
            context.Ack<TestEventHandler>();
            return ValueTask.CompletedTask;
        }
    }
}
