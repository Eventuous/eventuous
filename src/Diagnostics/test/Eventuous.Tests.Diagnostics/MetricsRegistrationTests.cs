using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Eventuous.Tests.Diagnostics;

[NotInParallel]
public class MetricsRegistrationTests : IDisposable {
    [Test]
    public async Task ShouldRegisterMetricsWithoutConstructorDiscovery() {
        ServiceDescriptor[] registrations = [];
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddEventuous()
            .AddEventuousSubscriptions()
            .ConfigureServices(services => registrations = services.Where(descriptor =>
                descriptor.ServiceType == typeof(CommandServiceMetrics)
                || descriptor.ServiceType == typeof(PersistenceMetrics)
                || descriptor.ServiceType == typeof(SubscriptionMetrics)).ToArray())
            .Build();

        await Assert.That(registrations.Length).IsEqualTo(3);
        foreach (var registration in registrations) {
            await Assert.That(registration.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
            await Assert.That(registration.ImplementationFactory).IsNotNull();
        }
    }

    [Test]
    public async Task ShouldMeasureRegisteredSubscriptionsWithCustomTags() {
        var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener {
            InstrumentPublished = (instrument, meterListener) => {
                if (instrument.Name == SubscriptionMetrics.GapCountMetricName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => measurements.Add((value, tags.ToArray().ToDictionary())));
        listener.Start();

        var services = new ServiceCollection();
        GetSubscriptionEndOfStream measure = _ => ValueTask.FromResult(new EndOfStream("test-subscription", 42, DateTime.UtcNow));
        services.AddSingleton(measure);
        services.AddOpenTelemetry().WithMetrics(builder => builder.AddEventuousSubscriptions(new TagList { { "tenant", "test" } }));
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<MeterProvider>();

        listener.RecordObservableInstruments();

        await Assert.That(measurements.Count).IsEqualTo(1);
        await Assert.That(measurements[0].Value).IsEqualTo(42);
        await Assert.That(measurements[0].Tags[SubscriptionMetrics.SubscriptionIdTag]).IsEqualTo("test-subscription");
        await Assert.That(measurements[0].Tags["tenant"]).IsEqualTo("test");
    }

    public void Dispose() => EventuousDiagnostics.RemoveDummyListener();
}
