using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public class DefaultConsumer : MessageConsumer {
    readonly IEventHandler[] _eventHandlers;

    public DefaultConsumer(IEventHandler[] eventHandlers) => _eventHandlers = eventHandlers;

    public override async ValueTask Consume(IMessageConsumeContext context) {
        try {
            if (context.Message == null) {
                context.Ignore<DefaultConsumer>();
                return;
            }

            var tasks = _eventHandlers.Select(Handle);
            await tasks.WhenAll().NoContext();
        }
        catch (Exception e) {
            context.Nack<DefaultConsumer>(e);
        }

        async ValueTask Handle(IEventHandler handler) {
            try {
                await handler.HandleEvent(context).NoContext();
            }
            catch (Exception e) {
                context.Nack(handler.GetType(), e);
            }
        }
    }
}