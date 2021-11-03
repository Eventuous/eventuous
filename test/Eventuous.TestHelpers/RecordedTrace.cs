using System.Diagnostics;

namespace Eventuous.TestHelpers;

public record RecordedTrace(
    ActivityTraceId? TraceId,
    ActivitySpanId?  SpanId,
    ActivitySpanId?  ParentSpanId
) {
    public const string DefaultTraceId = "00000000000000000000000000000000";
    public const string DefaultSpanId  = "0000000000000000";

    public bool IsDefaultTraceId => TraceId == null || TraceId.ToString() == DefaultTraceId;

    public bool IsDefaultSpanId => SpanId == null || SpanId.ToString() == DefaultSpanId;
}