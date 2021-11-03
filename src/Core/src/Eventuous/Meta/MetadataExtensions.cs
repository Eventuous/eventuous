namespace Eventuous;

public static class MetadataExtensions {
    public static Metadata WithCorrelationId(this Metadata metadata, string? correlationId)
        => metadata.With(MetaTags.CorrelationId, correlationId);

    public static Metadata WithCausationId(this Metadata metadata, string? causationId)
        => metadata.With(MetaTags.CausationId, causationId);

    public static string? GetCorrelationId(this Metadata metadata)
        => metadata.GetString(MetaTags.CorrelationId);

    public static string? GetCausationId(this Metadata metadata)
        => metadata.GetString(MetaTags.CausationId);
}