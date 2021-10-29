namespace Eventuous.Diagnostics;

public static class MetaTags {
    const string Prefix = "eventuous";

    public const string MessageId     = $"{Prefix}.message-id";
    public const string CorrelationId = $"{Prefix}.correlation-id";
    public const string CausationId   = $"{Prefix}.causation-id";
    public const string TraceId       = $"{Prefix}.trace-id";
    public const string SpanId        = $"{Prefix}.span-id";
    public const string ParentSpanId  = $"{Prefix}.parent-span-id";

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