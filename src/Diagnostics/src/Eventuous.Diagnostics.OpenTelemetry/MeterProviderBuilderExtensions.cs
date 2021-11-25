using Eventuous.Diagnostics.Metrics;
using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Eventuous.Diagnostics.OpenTelemetry;

[PublicAPI]
public static class MeterProviderBuilderExtensions {
    /// <summary>
    /// Adds subscriptions metrics instrumentation
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static MeterProviderBuilder AddEventuousSubscriptions(this MeterProviderBuilder builder) {
        Ensure.NotNull(builder);

        builder.AddMeter(SubscriptionMetrics.MeterName);
        builder.GetServices().AddSingleton<SubscriptionMetrics>();

        return builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder
            ? deferredMeterProviderBuilder.Configure(
                (sp, b) =>
                    b.AddInstrumentation(sp.GetRequiredService<SubscriptionMetrics>)
            ) : builder.AddInstrumentation<SubscriptionMetrics>();
    }

    /// <summary>
    /// Adds metrics instrumentation for core components such as application service and event store
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static MeterProviderBuilder AddEventuous(this MeterProviderBuilder builder) 
        => Ensure.NotNull(builder).AddMeter(EventuousMetrics.MeterName).AddInstrumentation<EventuousMetrics>();
}
