using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers; 

public interface IMessageConsumer<in TContext> where TContext : class, IMessageConsumeContext {
    ValueTask Consume(TContext context);
}

public interface IMessageConsumer : IMessageConsumer<IMessageConsumeContext> { }