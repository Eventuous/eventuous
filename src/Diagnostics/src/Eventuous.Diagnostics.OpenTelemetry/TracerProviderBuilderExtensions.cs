using OpenTelemetry.Trace;

namespace Eventuous.Diagnostics.OpenTelemetry;

[PublicAPI]
public static class TracerProviderBuilderExtensions {
    public static TracerProviderBuilder AddEventuousTracing(this TracerProviderBuilder builder) {
        EventuousDiagnostics.RemoveDummyListener();
        return Ensure.NotNull(builder, nameof(builder))
            .AddSource(EventuousDiagnostics.InstrumentationName);
    }
}