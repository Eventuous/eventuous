// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

using static DiagnosticTags;

public static class MetadataExtensions {
    public static Metadata AddActivityTags(this Metadata metadata, Activity? activity) {
        if (activity == null) return metadata;

        var tags = activity.Tags.Where(x => x.Value != null && MetaMappings.TelemetryToInternalTagsMap.ContainsKey(x.Key));

        foreach (var (key, value) in tags) {
            metadata.With(MetaMappings.TelemetryToInternalTagsMap[key], value!);
        }

        return metadata.AddTracingMeta(activity.GetTracingData());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Metadata AddTracingMeta(this Metadata metadata, TracingMeta tracingMeta)
        => metadata.ContainsKey(TraceId)
            ? metadata // don't override existing tracing data
            : metadata
                .AddNotNull(TraceId, tracingMeta.TraceId)
                .AddNotNull(SpanId, tracingMeta.SpanId)
                .AddNotNull(ParentSpanId, tracingMeta.ParentSpanId == EmptyId ? null : tracingMeta.ParentSpanId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TracingMeta GetTracingMeta(this Metadata metadata)
        => new(metadata.GetString(TraceId), metadata.GetString(SpanId), metadata.GetString(ParentSpanId));

    const string EmptyId = "0000000000000000";
}
