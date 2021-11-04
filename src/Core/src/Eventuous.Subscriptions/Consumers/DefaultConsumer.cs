using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public class DefaultConsumer : IMessageConsumer {
    readonly IEventHandler[] _eventHandlers;
    readonly bool            _throwOnError;

    public DefaultConsumer(IEventHandler[] eventHandlers, bool throwOnError) {
        _eventHandlers = eventHandlers;
        _throwOnError  = throwOnError;
    }

    public async ValueTask Consume(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        try {
            if (context.Message != null) {
                await Task.WhenAll(
                        _eventHandlers.Select(
                            x => x.HandleEvent(
                                context,
                                cancellationToken
                            )
                        )
                    )
                    .NoContext();
            }
        }
        catch (Exception e) {
            if (!_throwOnError) return;

            throw new SubscriptionException(
                context.Stream,
                context.MessageType,
                context.Message,
                e
            );
        }
    }
}