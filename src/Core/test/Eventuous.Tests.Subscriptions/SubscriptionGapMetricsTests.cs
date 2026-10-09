// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

[NotInParallel]
public class SubscriptionGapMetricsTests {
    [Test]
    public async Task Shared_source_keeps_individual_checkpoints_and_refreshes_each_collection() {
        await using var services = CreateServices(("first", "shared"), ("second", "shared"), ("other", "other"));
        using var metrics = new SubscriptionMetrics(services.GetServices<GetSubscriptionEndOfStream>());
        metrics.SetCustomTags(new TagList { { "custom", "value" } });
        using var diagnostic = new DiagnosticListener(CheckpointCommitHandler.DiagnosticName);
        var timestamp = DateTime.UtcNow;
        diagnostic.Write(CheckpointCommitHandler.CommitOperation, new CheckpointCommitHandler.CommitEvent("first", new(3, 0, timestamp), null));
        diagnostic.Write(CheckpointCommitHandler.CommitOperation, new CheckpointCommitHandler.CommitEvent("second", new(7, 0, timestamp), null));

        using var listener = new MeterListener();
        Dictionary<string, long> values = new();
        Dictionary<string, double> times = new();
        listener.InstrumentPublished = (instrument, owner) => {
            if (instrument.Meter.Name == SubscriptionMetrics.MeterName) owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => {
            if (instrument.Name != SubscriptionMetrics.GapCountMetricName) return;
            var copied = tags.ToArray();
            copied.ShouldContain(x => x.Key == "custom" && Equals(x.Value, "value"));
            values[(string)copied.Single(x => x.Key == SubscriptionMetrics.SubscriptionIdTag).Value!] = value;
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => {
            if (instrument.Name == SubscriptionMetrics.GapTimeMetricName)
                times[(string)tags.ToArray().Single(x => x.Key == SubscriptionMetrics.SubscriptionIdTag).Value!] = value;
        });
        listener.Start();
        listener.RecordObservableInstruments();

        values["first"].ShouldBe(7);
        values["second"].ShouldBe(3);
        values["other"].ShouldBe(10);
        times.Keys.ShouldContain("first");
        times.Keys.ShouldContain("second");
        times["first"].ShouldBe(times["second"]);
        var subscriptions = services.GetServices<MeasuredSubscription>().ToArray();
        subscriptions.Sum(x => x.Reads).ShouldBe(2);

        foreach (var subscription in subscriptions) subscription.Position = 20;
        listener.RecordObservableInstruments();
        values["first"].ShouldBe(17);
        values["second"].ShouldBe(13);
        subscriptions.Sum(x => x.Reads).ShouldBe(4);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Failed_shared_source_is_read_once_and_retried_next_collection(bool throws) {
        await using var services = CreateServices(("first", "shared"), ("second", "shared"), ("other", "other"));
        var subscriptions = services.GetServices<MeasuredSubscription>().ToArray();
        subscriptions[0].Fail = true;
        subscriptions[0].Throw = throws;
        using var metrics = new SubscriptionMetrics(services.GetServices<GetSubscriptionEndOfStream>());
        using var listener = Listen(out var values);
        listener.RecordObservableInstruments();
        values.Keys.ShouldBe(new[] { "other" });
        subscriptions.Sum(x => x.Reads).ShouldBe(2);

        subscriptions[0].Fail = false;
        listener.RecordObservableInstruments();
        values.Count.ShouldBe(3);
        subscriptions.Sum(x => x.Reads).ShouldBe(4);
    }

    [Test]
    public async Task Unkeyed_subscriptions_and_direct_delegates_keep_independent_reads() {
        await using var services = CreateServices(("first", null), ("second", null));
        var directReads = 0;
        GetSubscriptionEndOfStream direct = _ => {
            directReads++;
            return new(new EndOfStream("direct", 42, DateTime.UtcNow));
        };
        using var metrics = new SubscriptionMetrics([.. services.GetServices<GetSubscriptionEndOfStream>(), direct]);
        using var listener = Listen(out var values);
        listener.RecordObservableInstruments();
        values.Count.ShouldBe(3);
        values["direct"].ShouldBe(42);
        directReads.ShouldBe(1);
        services.GetServices<MeasuredSubscription>().ShouldAllBe(x => x.Reads == 1);
        ((IMeasuredSubscription)new LegacyMeasure()).EndOfStreamSourceKey.ShouldBeNull();
    }

    [Test]
    public async Task Checkpoint_advancing_past_shared_tail_reports_zero_then_refreshes() {
        await using var services = CreateServices(("first", "shared"), ("other", "other"), ("second", "shared"));
        var subscriptions = services.GetServices<MeasuredSubscription>().ToArray();
        using var metrics = new SubscriptionMetrics(services.GetServices<GetSubscriptionEndOfStream>());
        using var diagnostic = new DiagnosticListener(CheckpointCommitHandler.DiagnosticName);
        // Processing continues while an unrelated source is read between the two shared subscriptions.
        subscriptions[1].BeforeRead = () => {
            foreach (var subscription in subscriptions) subscription.Position = 30;
            diagnostic.Write(CheckpointCommitHandler.CommitOperation,
                new CheckpointCommitHandler.CommitEvent("second", new(30, 0, DateTime.UtcNow), null));
        };
        using var listener = Listen(out var values);
        listener.RecordObservableInstruments();
        values["second"].ShouldBe(0);
        subscriptions.Sum(x => x.Reads).ShouldBe(2);

        subscriptions[1].BeforeRead = null;
        foreach (var subscription in subscriptions) subscription.Position = 40;
        listener.RecordObservableInstruments();
        values["second"].ShouldBe(10);
        subscriptions.Sum(x => x.Reads).ShouldBe(4);
    }

    [Test]
    public async Task Shared_source_uses_configured_subscription_ids_for_checkpoints_and_tags() {
        var registrations = new ServiceCollection();
        foreach (var id in new[] { "first", "second" })
            registrations.AddSubscription<MeasuredSubscription, MeasureOptions>(id, builder => builder.Configure(options => {
                options.SubscriptionId = $"runtime-{id}";
                options.SourceKey = "shared";
            }));
        await using var services = registrations.BuildServiceProvider();
        using var metrics = new SubscriptionMetrics(services.GetServices<GetSubscriptionEndOfStream>());
        using var diagnostic = new DiagnosticListener(CheckpointCommitHandler.DiagnosticName);
        diagnostic.Write(CheckpointCommitHandler.CommitOperation,
            new CheckpointCommitHandler.CommitEvent("runtime-first", new(3, 0, DateTime.UtcNow), null));
        diagnostic.Write(CheckpointCommitHandler.CommitOperation,
            new CheckpointCommitHandler.CommitEvent("runtime-second", new(7, 0, DateTime.UtcNow), null));
        using var listener = Listen(out var values);
        listener.RecordObservableInstruments();

        values.Count.ShouldBe(2);
        values["runtime-first"].ShouldBe(7);
        values["runtime-second"].ShouldBe(3);
        services.GetServices<MeasuredSubscription>().Sum(x => x.Reads).ShouldBe(1);
    }

    static ServiceProvider CreateServices(params (string Id, string? Key)[] subscriptions) {
        var services = new ServiceCollection();
        foreach (var (id, key) in subscriptions)
            services.AddSubscription<MeasuredSubscription, MeasureOptions>(id, builder => builder.Configure(x => x.SourceKey = key));
        return services.BuildServiceProvider();
    }

    static MeterListener Listen(out Dictionary<string, long> values) {
        Dictionary<string, long> collected = new();
        values = collected;
        var listener = new MeterListener {
            InstrumentPublished = (instrument, owner) => {
                if (instrument.Meter.Name == SubscriptionMetrics.MeterName && instrument.Name == SubscriptionMetrics.GapCountMetricName)
                    owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            collected[(string)tags.ToArray().Single(x => x.Key == SubscriptionMetrics.SubscriptionIdTag).Value!] = value);
        listener.Start();
        return listener;
    }

    public record MeasureOptions : SubscriptionOptions {
        public string? SourceKey { get; set; }
    }

    public class MeasuredSubscription(MeasureOptions options, ConsumePipe pipe)
        : EventSubscription<MeasureOptions>(options, pipe, NullLoggerFactory.Instance, null), IMeasuredSubscription {
        public int Reads { get; private set; }
        public ulong Position { get; set; } = 10;
        public bool Fail { get; set; }
        public bool Throw { get; set; }
        public Action? BeforeRead { get; set; }
        public object? EndOfStreamSourceKey => Options.SourceKey;
        protected override ValueTask Connect(SubscriptionRun run) => default;
        public GetSubscriptionEndOfStream GetMeasure() => _ => {
            Reads++;
            BeforeRead?.Invoke();
            if (Fail && Throw) throw new InvalidOperationException("Unavailable source");
            return new(Fail ? EndOfStream.Invalid : new(SubscriptionId, Position, DateTime.UtcNow));
        };
    }

    sealed class LegacyMeasure : IMeasuredSubscription {
        public GetSubscriptionEndOfStream GetMeasure() => _ => new(EndOfStream.Invalid);
    }
}
