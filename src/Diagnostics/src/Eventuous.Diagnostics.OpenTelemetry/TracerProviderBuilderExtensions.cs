using OpenTelemetry.Trace;

namespace Eventuous.Diagnostics.OpenTelemetry;

[PublicAPI]
public static class TracerProviderBuilderExtensions {
    /// <summary>
    /// Adds Eventuous activity source to OpenTelemetry trace collection
    /// </summary>
    /// <param name="builder"><seealso cref="TracerProviderBuilder"/> instance</param>
    /// <returns></returns>
    public static TracerProviderBuilder AddEventuousTracing(this TracerProviderBuilder builder) {
        // The DummyListener is added by default so the remote context is propagated regardless.
        // After adding the activity source to OpenTelemetry we don't need the dummy listener.
        EventuousDiagnostics.RemoveDummyListener();

        return Ensure.NotNull(builder)
            .AddSource(EventuousDiagnostics.InstrumentationName);
    }
}
