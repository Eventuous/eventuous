using System.Diagnostics;

namespace Eventuous.Diagnostics;

public static class MetadataExtensions {
    public static Metadata AddActivityTags(this Metadata metadata, Activity? activity) {
        if (activity == null) return metadata;

        var tags = activity.Tags
            .Where(x => x.Value != null && MetaMappings.TelemetryToInternalTagsMap.ContainsKey(x.Key));

        foreach (var (key, value) in tags) {
            metadata.Add(MetaMappings.TelemetryToInternalTagsMap[key], value!);
        }
        metadata.Add(MetaTags.TraceId, activity.TraceId.ToString());
        metadata.Add(MetaTags.SpanId, activity.SpanId.ToString());
        metadata.Add(MetaTags.ParentSpanId, activity.ParentSpanId.ToString());

        return metadata;
    }
}