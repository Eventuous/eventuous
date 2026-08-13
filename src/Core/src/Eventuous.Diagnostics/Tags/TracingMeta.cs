// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

public record TracingMeta(string? TraceId, string? SpanId) {
    /// <summary>
    /// Whether the metadata carries a tracing context that can be used: both ids present and neither all-zero.
    /// An all-zero id is what an activity reports when it was never given a real one, and persisting or
    /// restoring that is worse than having nothing, so both ends of the round trip check it here.
    /// </summary>
    internal bool IsValid() => TraceId is not (null or EmptyTraceId) && SpanId is not (null or EmptySpanId);

    /// <summary>
    /// Restores an activity context from the persisted tracing metadata, or <c>null</c> when the metadata carries
    /// no usable context. Callers check for null to decide whether there is anything to correlate with, so an
    /// absent or all-zero context must not come back as a zeroed <seealso cref="ActivityContext"/>.
    /// </summary>
    public ActivityContext? ToActivityContext(bool isRemote) {
        try {
            return IsValid()
                ? new ActivityContext(
                    ActivityTraceId.CreateFromString(TraceId),
                    ActivitySpanId.CreateFromString(SpanId),
                    ActivityTraceFlags.Recorded,
                    isRemote: isRemote
                )
                : null;
        }
        catch (Exception) {
            return null;
        }
    }

    const string EmptyTraceId = "00000000000000000000000000000000";
    const string EmptySpanId  = "0000000000000000";
}
