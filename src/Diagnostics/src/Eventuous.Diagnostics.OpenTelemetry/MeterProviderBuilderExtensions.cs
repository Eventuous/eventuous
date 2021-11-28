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
    public static MeterProviderBuilder AddEventuousSubscriptions(this MeterProviderBuilder builder)
        => Ensure.NotNull(builder)
            .AddMeter(SubscriptionMetrics.MeterName)
            .AddMetrics<SubscriptionMetrics>();

    /// <summary>
    /// Adds metrics instrumentation for core components such as application service and event store
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static MeterProviderBuilder AddEventuous(this MeterProviderBuilder builder)
        => Ensure.NotNull(builder)
            .AddMeter(EventuousMetrics.MeterName)
            .AddMetrics<EventuousMetrics>();

    static MeterProviderBuilder AddMetrics<T>(this MeterProviderBuilder builder) where T : class {
        builder.GetServices().AddSingleton<T>();

        return builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder
            ? deferredMeterProviderBuilder.Configure(
                (sp, b) =>
                    b.AddInstrumentation(sp.GetRequiredService<T>)
            ) : builder.AddInstrumentation<T>();
    }
}