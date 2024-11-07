// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Eventuous.Diagnostics.OpenTelemetry;

[PublicAPI]
public static class MeterProviderBuilderExtensions {
    /// <summary>
    /// Adds subscription metrics instrumentation
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="customTags"></param>
    /// <returns></returns>
    public static MeterProviderBuilder AddEventuousSubscriptions(this MeterProviderBuilder builder, TagList? customTags = null)
        => Ensure.NotNull(builder).AddMeter(SubscriptionMetrics.MeterName).AddMetrics<SubscriptionMetrics>(customTags);

    /// <summary>
    /// Adds metrics instrumentation for core components such as application service and event store
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="customTags"></param>
    /// <returns></returns>
    public static MeterProviderBuilder AddEventuous(this MeterProviderBuilder builder, TagList? customTags = null)
        => Ensure.NotNull(builder)
            .AddMeter(CommandServiceMetrics.MeterName)
            .AddMetrics<CommandServiceMetrics>(customTags)
            .AddMeter(PersistenceMetrics.MeterName)
            .AddMetrics<PersistenceMetrics>(customTags);

    static MeterProviderBuilder AddMetrics<T>(this MeterProviderBuilder builder, TagList? customTags = null)
        where T : class, IWithCustomTags {
        builder.ConfigureServices(services => services.AddSingleton<T>());

        return builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder
            ? deferredMeterProviderBuilder.Configure(
                (sp, b) => {
                    b.AddInstrumentation(
                        () => {
                            var instrument = sp.GetRequiredService<T>();
                            if (customTags != null) instrument.SetCustomTags(customTags.Value);

                            return instrument;
                        }
                    );
                }
            )
            : builder;
    }
}
