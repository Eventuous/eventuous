using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

static class GatewayMetaHelper {
    public static Metadata GetMeta(this GatewayMessage gatewayMessage, IMessageConsumeContext context) {
        var (_, _, metadata) = gatewayMessage;
        var meta = metadata == null ? new Metadata() : new Metadata(metadata);
        return meta.WithCausationId(context.MessageId);
    }
}