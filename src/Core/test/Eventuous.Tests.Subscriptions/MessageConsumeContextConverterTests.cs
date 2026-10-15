using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Tests.Subscriptions;

public class MessageConsumeContextConverterTests {
    /// <summary>
    /// A regression smoke test, not proof of thread-safety. Many workers convert messages of types nobody converted
    /// before while converters get registered mid-flight, the way a module initializer of a late-loaded assembly would.
    /// Every conversion must still produce the right typed context and nothing may throw. A race, if reintroduced,
    /// would show up here only probabilistically.
    /// Converter registrations are process-wide and can't be undone without changing production code, so the
    /// no-op converters stay registered; the conversion cache entries added here are removed again.
    /// </summary>
    [Test]
    public async Task ShouldConvertConcurrentlyWhileCachingAndRegistering() {
        // List<T> over distinct T gives hundreds of message types that no other test converts
        var messageTypes = typeof(object).Assembly.GetExportedTypes()
            .Where(t => t is { ContainsGenericParameters: false, IsByRefLike: false, IsPointer: false } && t != typeof(void) && !(t.IsAbstract && t.IsSealed))
            .Take(500)
            .Select(t => typeof(List<>).MakeGenericType(t))
            .ToArray();

        const int workers = 8;
        using var start   = new Barrier(workers + 1);

        try {
            await RunConcurrently(messageTypes, workers, start);
        } finally {
            foreach (var messageType in messageTypes) MessageConsumeContextConverter.ConversionCache.TryRemove(messageType, out _);
        }
    }

    static async Task RunConcurrently(Type[] messageTypes, int workers, Barrier start) {
        var conversions = Enumerable.Range(0, workers)
            .Select(
                worker => Task.Run(
                    () => {
                        start.SignalAndWait();

                        // Each worker walks the types from a different offset, so the same types are
                        // being added by one worker while another is looking them up
                        for (var i = 0; i < messageTypes.Length; i++) {
                            var messageType = messageTypes[(i + worker * messageTypes.Length / workers) % messageTypes.Length];
                            var message     = Activator.CreateInstance(messageType)!;
                            var typed       = CreateContext(message).ConvertToGeneric();

                            var expected = typeof(MessageConsumeContext<>).MakeGenericType(messageType);

                            if (typed.GetType() != expected || !ReferenceEquals(typed.Message, message)) {
                                throw new InvalidOperationException($"Expected {expected}, got {typed.GetType()}");
                            }
                        }
                    }
                )
            )
            .ToArray();

        var registrations = Task.Run(
            () => {
                start.SignalAndWait();

                // Converters that handle nothing, so conversions still fall through to the cache
                for (var i = 0; i < 8; i++) {
                    MessageConsumeContextConverter.Register(_ => null);
                    Thread.Yield();
                }
            }
        );

        await Task.WhenAll([..conversions, registrations]);
    }

    static MessageConsumeContext CreateContext(object message)
        => new(
            Guid.NewGuid().ToString(),
            message.GetType().Name,
            "application/json",
            "test-stream",
            0,
            0,
            0,
            0,
            DateTime.UtcNow,
            message,
            null,
            "test-subscription",
            CancellationToken.None
        );
}
