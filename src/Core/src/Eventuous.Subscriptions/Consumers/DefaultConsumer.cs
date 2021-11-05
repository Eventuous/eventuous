using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public class DefaultConsumer : IMessageConsumer {
    readonly IEventHandler[] _eventHandlers;

    public DefaultConsumer(IEventHandler[] eventHandlers) => _eventHandlers = eventHandlers;

    public async ValueTask Consume(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        try {
            if (context.Message == null) {
                context.Ignore<DefaultConsumer>();
                return;
            }

            var tasks = _eventHandlers.Select(Handle);
            await tasks.WhenAll();
        }
        catch (Exception e) {
            context.Nack<DefaultConsumer>(e);
        }

        async ValueTask Handle(IEventHandler handler) {
            try {
                await handler.HandleEvent(context, cancellationToken);
            }
            catch (Exception e) {
                context.Nack(handler.GetType(), e);
            }
        }
    }
}