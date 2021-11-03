namespace Eventuous; 

public static class MetaTags {
    const string Prefix = "eventuous";

    public const string MessageId     = $"{Prefix}.message-id";
    public const string CorrelationId = $"{Prefix}.correlation-id";
    public const string CausationId   = $"{Prefix}.causation-id";
    public const string TraceId       = $"{Prefix}.trace-id";
    public const string SpanId        = $"{Prefix}.span-id";
    public const string ParentSpanId  = $"{Prefix}.parent-span-id";
}