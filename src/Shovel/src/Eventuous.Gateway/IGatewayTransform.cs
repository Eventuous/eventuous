using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

public interface IGatewayTransform {
    ValueTask<GatewayContext?> RouteAndTransform(IMessageConsumeContext context);
}

public interface IGatewayTransform<TProduceOptions> {
    ValueTask<GatewayContext<TProduceOptions>?> RouteAndTransform(IMessageConsumeContext context);
}
