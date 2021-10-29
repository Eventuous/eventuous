using static Eventuous.MetaTags;

namespace Eventuous.Diagnostics;

public static class MetaMappings {
    public static readonly IDictionary<string, string> TelemetryToInternalTagsMap = new Dictionary<string, string> {
        { TelemetryTags.Message.Id, MessageId },
        { TelemetryTags.Messaging.CausationId, CausationId },
        { TelemetryTags.Messaging.CorrelationId, CorrelationId }
    };

    public static readonly IDictionary<string, string> InternalToTelemetryTagsMap = new Dictionary<string, string> {
        { MessageId, TelemetryTags.Message.Id },
        { CausationId, TelemetryTags.Messaging.CausationId },
        { CorrelationId, TelemetryTags.Messaging.CorrelationId }
    };
}