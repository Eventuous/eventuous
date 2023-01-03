namespace Eventuous;

public static class MetaTags {
    const string Prefix = "eventuous";

    public const string MessageId     = $"{Prefix}.message-id";
    public const string CorrelationId = $"{Prefix}.correlation-id";
    public const string CausationId   = $"{Prefix}.causation-id";
}
