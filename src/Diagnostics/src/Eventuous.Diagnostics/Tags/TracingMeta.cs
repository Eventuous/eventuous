using System.Diagnostics;

namespace Eventuous.Diagnostics;

public record TracingMeta(string? TraceId, string? SpanId, string? ParentSpanId) {
    bool IsValid() => TraceId != null && SpanId != null;

    public ActivityContext? ToActivityContext(bool isRemote) {
        try {
            return IsValid() ?
                new ActivityContext(
                    ActivityTraceId.CreateFromString(TraceId),
                    ActivitySpanId.CreateFromString(SpanId),
                    ActivityTraceFlags.Recorded,
                    isRemote: isRemote
                ) : default;
        }
        catch (Exception) {
            return default;
        }
    }
}