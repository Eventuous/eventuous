// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class MetadataExtensions {
    /// <param name="metadata">Metadata instance</param>
    extension(Metadata metadata) {
        /// <summary>
        /// Add correlation id to metadata
        /// </summary>
        /// <param name="correlationId">Correlation id value</param>
        /// <returns></returns>
        public Metadata WithCorrelationId(string? correlationId) => metadata.With(MetaTags.CorrelationId, correlationId);

        /// <summary>
        /// Add causation id to metadata
        /// </summary>
        /// <param name="causationId">Causation id value</param>
        /// <returns></returns>
        public Metadata WithCausationId(string? causationId) => metadata.With(MetaTags.CausationId, causationId);

        /// <summary>
        /// Get the correlation id from metadata, if available
        /// </summary>
        /// <returns>Correlation id or null</returns>
        public string? GetCorrelationId() => metadata.GetString(MetaTags.CorrelationId);

        /// <summary>
        /// Get the causation id from metadata, if available
        /// </summary>
        /// <returns>Causation id or null</returns>
        public string? GetCausationId() => metadata.GetString(MetaTags.CausationId);
    }
}
