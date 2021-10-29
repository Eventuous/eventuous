using System.Diagnostics;
using Eventuous.Diagnostics;

namespace Eventuous.Producers.Diagnostics;

public static class ProducerActivity {
    public static (Activity?, ProducedMessage) Start(
        ProducedMessage                             message,
        IEnumerable<KeyValuePair<string, object?>>? tags
    ) {
        var activity = GetActivity(tags);

        var (msg, metadata) = message;
        var meta      = GetMeta(metadata);
        var messageId = meta.GetMessageId();

        activity?
            .SetTag(TelemetryTags.Message.Type, TypeMap.GetTypeName(msg))
            .SetTag(TelemetryTags.Message.Id, messageId.ToString())
            .SetTag(TelemetryTags.Messaging.MessageId, messageId.ToString())
            .CopyParentTag(TelemetryTags.Messaging.ConversationId)
            .SetOrCopyParentTag(TelemetryTags.Messaging.CausationId, meta.GetCausationId(), TelemetryTags.Message.Id)
            .SetOrCopyParentTag(TelemetryTags.Messaging.CorrelationId, meta.GetCorrelationId())
            .Start();

        meta.AddActivityTags(activity);

        return (activity, new ProducedMessage(msg, meta));
    }

    public static (Activity?, IEnumerable<ProducedMessage>) Start(
        IEnumerable<ProducedMessage>                messages,
        IEnumerable<KeyValuePair<string, object?>>? tags
    ) {
        var activity = GetActivity(tags);

        activity?
            .CopyParentTag(TelemetryTags.Messaging.ConversationId)
            .CopyParentTag(TelemetryTags.Messaging.CausationId)
            .CopyParentTag(TelemetryTags.Messaging.CausationId, TelemetryTags.Message.Id)
            .CopyParentTag(TelemetryTags.Messaging.CorrelationId)
            .Start();

        return (activity, messages.Select(GetMessage));

        ProducedMessage GetMessage(ProducedMessage message) {
            var (msg, metadata) = message;
            return new ProducedMessage(msg, GetMeta(metadata).AddActivityTags(activity));
        }
    }

    static Activity? GetActivity(IEnumerable<KeyValuePair<string, object?>>? tags)
        => SharedDiagnostics.ActivitySource.CreateActivity(
            "produce",
            ActivityKind.Producer,
            parentContext: default,
            tags
        );

    static Metadata GetMeta(Metadata? metadata) {
        var messageId = metadata?.GetMessageId() ?? Guid.NewGuid();

        return Metadata
            .FromMeta(metadata)
            .WithMessageId(messageId)
            .WithCorrelationId(metadata?.GetCorrelationId());
    }
}