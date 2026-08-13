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
        // The DummyListener is added by default, so the remote context is propagated regardless.
        // After adding the activity source to OpenTelemetry, we don't need a fake listener.
        EventuousDiagnostics.RemoveDummyListener();

        return Ensure.NotNull(builder).AddSource(EventuousDiagnostics.InstrumentationName);
    }
}
