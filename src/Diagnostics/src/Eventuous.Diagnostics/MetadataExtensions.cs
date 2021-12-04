using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Eventuous.Diagnostics.DiagnosticTags;

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
            metadata.With(MetaMappings.TelemetryToInternalTagsMap[key], value!);
        }

        return metadata.AddTracingMeta(activity.GetTracingData());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Metadata AddTracingMeta(this Metadata metadata, TracingMeta tracingMeta)
        => metadata
            .AddNotNull(TraceId, tracingMeta.TraceId)
            .AddNotNull(SpanId, tracingMeta.SpanId)
            .AddNotNull(ParentSpanId, tracingMeta.ParentSpanId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TracingMeta GetTracingMeta(this Metadata metadata)
        => new(
            metadata.GetString(TraceId),
            metadata.GetString(SpanId),
            metadata.GetString(ParentSpanId)
        );
}