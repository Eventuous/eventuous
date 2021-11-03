using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Eventuous.Diagnostics;

public static class MetadataExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Metadata AddActivityTags(this Metadata metadata, Activity? activity) {
        if (activity == null) return metadata;

        var tags = activity.Tags
            .Where(
                x => x.Value != null && MetaMappings.TelemetryToInternalTagsMap.ContainsKey(x.Key)
            );

        foreach (var (key, value) in tags) {
            metadata.Add(MetaMappings.TelemetryToInternalTagsMap[key], value!);
        }

        return metadata.AddTracingMeta(activity.GetTracingData());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Metadata AddTracingMeta(this Metadata metadata, TracingMeta tracingMeta)
        => metadata.AddNotNull(MetaTags.TraceId, tracingMeta.TraceId)
            .AddNotNull(MetaTags.SpanId, tracingMeta.SpanId)
            .AddNotNull(MetaTags.ParentSpanId, tracingMeta.ParentSpanId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TracingMeta GetTracingMeta(this Metadata metadata)
        => new(
            metadata.GetString(MetaTags.TraceId),
            metadata.GetString(MetaTags.SpanId),
            metadata.GetString(MetaTags.ParentSpanId)
        );
}