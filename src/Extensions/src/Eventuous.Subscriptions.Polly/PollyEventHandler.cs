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

    public Task HandleEvent(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    )
        => _retryPolicy.ExecuteAsync(() => _inner.HandleEvent(context, cancellationToken));
}