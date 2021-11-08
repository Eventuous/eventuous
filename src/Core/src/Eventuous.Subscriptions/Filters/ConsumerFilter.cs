using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public class ConsumerFilter<TConsumer, TContext> : ConsumeFilter<TContext>
    where TContext : class, IMessageConsumeContext
    where TConsumer : IMessageConsumer<TContext> {
    readonly TConsumer _consumer;

    public ConsumerFilter(TConsumer consumer) => _consumer = consumer;

    public override ValueTask Send(TContext context, Func<TContext, ValueTask>? next)
        => _consumer.Consume(context);
}