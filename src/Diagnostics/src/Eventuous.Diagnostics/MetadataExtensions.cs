using System.Diagnostics;

namespace Eventuous.Diagnostics;

public static class MetadataExtensions {
    public static Metadata WithMessageId(this Metadata metadata, Guid messageId)
        => metadata.With(MetaTags.MessageId, messageId);

    public static Metadata WithCorrelationId(this Metadata metadata, string? correlationId)
        => metadata.With(MetaTags.CorrelationId, correlationId);

    public static Metadata WithCausationId(this Metadata metadata, string? causationId)
        => metadata.With(MetaTags.CausationId, causationId);

    public static Guid GetMessageId(this Metadata metadata) => metadata.Get<Guid>(MetaTags.MessageId);
    
    public static Guid GetOrSetMessageId(this Metadata metadata) {
        if (metadata.ContainsKey(MetaTags.MessageId)) 
            return metadata.Get<Guid>(MetaTags.MessageId);

        var messageId = Guid.NewGuid();
        metadata.WithMessageId(messageId);
        return messageId;
    }

    public static string? GetCorrelationId(this Metadata metadata) => metadata.GetString(MetaTags.CorrelationId);

    public static string? GetCausationId(this Metadata metadata) => metadata.GetString(MetaTags.CausationId);

    public static Metadata AddActivityTags(this Metadata metadata, Activity? activity) {
        if (activity == null) return metadata;

        var tags = activity.Tags
            .Where(x => x.Value != null && MetaTags.TelemetryToInternalTagsMap.ContainsKey(x.Key));

        foreach (var (key, value) in tags) {
            metadata.Add(MetaTags.TelemetryToInternalTagsMap[key], value!);
        }
        metadata.Add(MetaTags.TraceId, activity.TraceId.ToString());
        metadata.Add(MetaTags.SpanId, activity.SpanId.ToString());
        metadata.Add(MetaTags.ParentSpanId, activity.ParentSpanId.ToString());

        return metadata;
    }
}