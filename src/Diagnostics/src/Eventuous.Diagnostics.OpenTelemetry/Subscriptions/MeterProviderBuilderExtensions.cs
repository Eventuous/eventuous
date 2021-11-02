using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Eventuous.Diagnostics.OpenTelemetry.Subscriptions;

[PublicAPI]
public static class MeterProviderBuilderExtensions {
    /// <summary>
    /// Adds subscriptions metrics instrumentation
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static MeterProviderBuilder AddEventuousSubscriptions(this MeterProviderBuilder builder) {
        Ensure.NotNull(builder, nameof(builder));

        builder.GetServices().AddSingleton<SubscriptionGapMetric>();
        builder.AddMeter(EventuousDiagnostics.GetMeterName(SubscriptionGapMetric.MeterName));

        return builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder
            ? deferredMeterProviderBuilder.Configure(
                (sp, b) => b.AddInstrumentation(sp.GetRequiredService<SubscriptionGapMetric>)
            ) : builder.AddInstrumentation<SubscriptionGapMetric>();
    }
}