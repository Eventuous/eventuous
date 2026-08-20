// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;
using Shouldly;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Covers <see cref="AsyncHandlingFilter"/> alone, not any particular transport: a nack belongs to the
/// subscription, so this filter must survive one that throws. Its reader task is observed by nothing until
/// dispose, so losing it costs the subscription its consumption while leaving it looking healthy.
/// </summary>
/// <remarks>
/// The transports are covered where they live — RabbitMQ's version of this failure is
/// <c>Eventuous.Tests.RabbitMq.HandlerFailureSpec</c>, against a real broker. What's pinned here is only that
/// a third-party transport whose nack throws cannot take the shared worker down with it.
/// </remarks>
public class ThrowingNackTests {
    [Test]
    public async Task A_nack_that_throws_leaves_the_channel_worker_reading(CancellationToken ct) {
        var loggerFactory = LoggingExtensions.GetLoggerFactory();
        var handler       = new FailFirstHandler();

        var subscription = new ThrowingNackSubscription(handler, loggerFactory, messages: 3);

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        // The first message fails and its nack throws; the two behind it prove the reader is still there.
        var handled = await WaitFor(() => handler.Handled >= 3, ct);

        handled.ShouldBeTrue($"the worker stopped reading after the throwing nack: {handler.Handled} of 3 messages handled");
        subscription.Acked.ShouldBe(2, "the two messages that succeeded should have been acknowledged");
        subscription.NacksThrown.ShouldBe(1, "exactly the failing message should have reached the throwing nack");

        await subscription.Unsubscribe(_ => { }, ct);
        await subscription.DisposeAsync();
    }

    static async Task<bool> WaitFor(Func<bool> condition, CancellationToken ct) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try {
            while (!condition()) await Task.Delay(20, cts.Token).NoContext();

            return true;
        } catch (OperationCanceledException) {
            return condition();
        }
    }

    sealed class FailFirstHandler : BaseEventHandler {
        int _handled;

        public int Handled => Volatile.Read(ref _handled);

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            var count = Interlocked.Increment(ref _handled);

            return count == 1
                ? throw new InvalidOperationException("Simulated handler failure on the first message")
                : new(EventHandlingStatus.Success);
        }
    }

    record TestOptions : SubscriptionOptions;

    /// <summary>
    /// Shaped like the transports that own their acknowledgements — a pipe fronted by
    /// <see cref="AsyncHandlingFilter"/> and a nack that throws under <c>ThrowOnError</c>. Synthetic on
    /// purpose: no transport in this repo throws from a nack any more, which is exactly why the filter's own
    /// guard needs a test of its own.
    /// </summary>
    sealed class ThrowingNackSubscription(IEventHandler handler, ILoggerFactory loggerFactory, int messages)
        : EventSubscription<TestOptions>(
            new() { SubscriptionId = $"throwing-nack-{Guid.NewGuid()}", ThrowOnError = true },
            new ConsumePipe().AddDefaultConsumer(handler).AddFilterFirst(new AsyncHandlingFilter(1)),
            loggerFactory,
            null
        ) {
        int _acked;
        int _nacksThrown;

        public int Acked       => Volatile.Read(ref _acked);
        public int NacksThrown => Volatile.Read(ref _nacksThrown);

        protected override ValueTask Connect(SubscriptionRun run) {
            var pumping = Task.Run(() => Pump(run), CancellationToken.None);
            run.OnDisconnect(_ => new(pumping));

            return default;
        }

        async Task Pump(SubscriptionRun run) {
            for (var i = 0; i < messages && !run.Token.IsCancellationRequested; i++) {
                var context = new MessageConsumeContext(
                    Guid.NewGuid().ToString(),
                    "TestEvent",
                    "application/json",
                    "test-stream",
                    (ulong)i,
                    (ulong)i,
                    (ulong)i,
                    run.NextSequence(),
                    DateTime.UtcNow,
                    new { Number = i },
                    new(),
                    Options.SubscriptionId,
                    run.Token
                ) { LogContext = Log };

                await Handler(new AsyncConsumeContext(context, Ack, Nack)).NoContext();
            }

            // Parked, not returned: a pump that ends while its connection is up reads as a drop.
            await run.Ended.NoContext();
        }

        ValueTask Ack(IMessageConsumeContext context) {
            Interlocked.Increment(ref _acked);

            return default;
        }

        ValueTask Nack(IMessageConsumeContext context, Exception exception) {
            Interlocked.Increment(ref _nacksThrown);

            throw exception;
        }
    }
}
