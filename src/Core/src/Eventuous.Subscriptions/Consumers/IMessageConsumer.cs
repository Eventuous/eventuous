using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers; 

public interface IMessageConsumer<in TContext> where TContext : class, IMessageConsumeContext {
    ValueTask Consume(TContext context);
}

public abstract class MessageConsumer : IMessageConsumer<IMessageConsumeContext> {
    public abstract ValueTask Consume(IMessageConsumeContext context);
}