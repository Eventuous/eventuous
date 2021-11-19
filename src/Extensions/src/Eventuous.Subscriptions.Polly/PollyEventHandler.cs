using Eventuous.Subscriptions.Context;
using Polly;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;
using PollyContext = Polly.Context;

namespace Eventuous.Subscriptions.Polly;

/// <summary>
/// Wrapping handler to execute the inner handler with a given retry policy
/// </summary>
public class PollyEventHandler : IEventHandler {
    readonly IEventHandler _inner;
    readonly IAsyncPolicy  _retryPolicy;
    readonly string        _innerName;

    public PollyEventHandler(IEventHandler inner, IAsyncPolicy retryPolicy) {
        _inner       = inner;
        _innerName   = inner.GetType().Name;
        _retryPolicy = retryPolicy;
    }

    public async ValueTask HandleEvent(IMessageConsumeContext context) {
        const string retryKey = "eventuous-retry";
        
        var pollyContext = new PollyContext { { retryKey, new RetryCounter() } };
        await _retryPolicy.ExecuteAsync(Execute, pollyContext).NoContext();

        async Task Execute(PollyContext ctx) {
            try {
                await _inner.HandleEvent(context).NoContext();
            }
            catch (Exception e) {
                var counter = ctx[retryKey] as RetryCounter;
                Log.FailedToHandleMessageWithRetry(_innerName, context.MessageType, counter!.Counter, e);
                counter.Increase();
                throw;
            }
        }
    }

    class RetryCounter {
        public int Counter { get; private set; }

        public void Increase() => Counter++;
    }
}