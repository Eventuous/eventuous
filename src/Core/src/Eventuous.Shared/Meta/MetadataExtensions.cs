// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class MetadataExtensions {
    /// <summary>
    /// Add correlation id to metadata
    /// </summary>
    /// <param name="metadata">Metadata instance</param>
    /// <param name="correlationId">Correlation id value</param>
    /// <returns></returns>
    public static Metadata WithCorrelationId(this Metadata metadata, string? correlationId) => metadata.With(MetaTags.CorrelationId, correlationId);

    /// <summary>
    /// Add causation id to metadata
    /// </summary>
    /// <param name="metadata">Metadata instance</param>
    /// <param name="causationId">Causation id value</param>
    /// <returns></returns>
    public static Metadata WithCausationId(this Metadata metadata, string? causationId) => metadata.With(MetaTags.CausationId, causationId);

    /// <summary>
    /// Get the correlation id from metadata, if available
    /// </summary>
    /// <param name="metadata">Metadata instance</param>
    /// <returns>Correlation id or null</returns>
    public static string? GetCorrelationId(this Metadata metadata) => metadata.GetString(MetaTags.CorrelationId);

    /// <summary>
    /// Get the causation id from metadata, if available
    /// </summary>
    /// <param name="metadata">Metadata instance</param>
    /// <returns>Causation id or null</returns>
    public static string? GetCausationId(this Metadata metadata) => metadata.GetString(MetaTags.CausationId);
}
