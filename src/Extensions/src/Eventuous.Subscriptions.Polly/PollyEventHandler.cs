using Eventuous.Subscriptions.Context;
using Polly;

namespace Eventuous.Subscriptions.Polly;

/// <summary>
/// Wrapping handler to execute the inner handler with a given retry policy
/// </summary>
public class PollyEventHandler : IEventHandler {
    readonly IEventHandler _inner;
    readonly IAsyncPolicy  _retryPolicy;

    public PollyEventHandler(IEventHandler inner, IAsyncPolicy retryPolicy) {
        _inner       = inner;
        _retryPolicy = retryPolicy;
    }

    public async ValueTask HandleEvent(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    )
        => await _retryPolicy.ExecuteAsync(
            async () => await _inner.HandleEvent(context, cancellationToken)
        );
}