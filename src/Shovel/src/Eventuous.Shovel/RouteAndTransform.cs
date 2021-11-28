using Eventuous.Subscriptions.Context;

namespace Eventuous.Shovel;

public delegate ValueTask<ShovelContext?> RouteAndTransform(IMessageConsumeContext context);

public delegate ValueTask<ShovelContext<TProduceOptions>?> RouteAndTransform<TProduceOptions>(
    IMessageConsumeContext message
);