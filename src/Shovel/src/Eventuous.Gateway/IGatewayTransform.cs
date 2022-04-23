using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

public interface IGatewayTransform {
    ValueTask<GatewayMessage[]> RouteAndTransform(IMessageConsumeContext context);
}

public interface IGatewayTransform<TProduceOptions> {
    ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform(IMessageConsumeContext context);
}
