using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers; 

public interface IMessageConsumer {
    ValueTask Consume(IMessageConsumeContext context, CancellationToken cancellationToken);
}