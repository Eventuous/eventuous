// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using OpenTelemetry.Trace;

namespace Eventuous.Diagnostics.OpenTelemetry;

[PublicAPI]
public static class TracerProviderBuilderExtensions {
    /// <summary>
    /// Adds an Eventuous activity source to OpenTelemetry trace collection. Sampling is left to the application:
    /// this only registers the source, so whatever sampler is configured on the provider stays in effect.
    /// </summary>
    /// <param name="builder"><seealso cref="TracerProviderBuilder"/> instance</param>
    /// <returns></returns>
    public static TracerProviderBuilder AddEventuousTracing(this TracerProviderBuilder builder) {
        Ensure.NotNull(builder);
        EventuousDiagnostics.RemoveDummyListener();

        return builder.AddSource(EventuousDiagnostics.InstrumentationName);
    }
}
