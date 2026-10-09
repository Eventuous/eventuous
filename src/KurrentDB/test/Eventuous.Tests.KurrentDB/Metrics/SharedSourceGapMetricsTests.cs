// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Metrics;
using System.Net.Http;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Registrations;
using Eventuous.Tools;
using global::KurrentDB.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eventuous.Tests.KurrentDB.Metrics;

[NotInParallel]
public class SharedSourceGapMetricsTests {
    [Test]
    public async Task All_stream_subscriptions_read_shared_tail_once_per_collection(CancellationToken cancellationToken) {
        // Standard projections append link events asynchronously, making the global tail nondeterministic.
        await using var container = KurrentDBContainer.CreateBuilder()
            .WithEnvironment("KURRENTDB_RUN_PROJECTIONS", "None")
            .WithEnvironment("KURRENTDB_START_STANDARD_PROJECTIONS", "false")
            .Build();
        await container.StartAsync(cancellationToken).NoContext();
        using var writer = new KurrentDBClient(KurrentDBClientSettings.Create(container.GetConnectionString()));
        var stream = $"shared-gap-{Guid.NewGuid():N}";
        // KurrentDB global positions are byte offsets, not event counts. Use actual append positions.
        var first = await Append().NoContext();
        var second = await Append().NoContext();
        var tail = await Append().NoContext();

        using var requests = new TailReadCounter();
        var settings = KurrentDBClientSettings.Create(container.GetConnectionString());
        settings.CreateHttpMessageHandler = () => requests;
        using var client = new KurrentDBClient(settings);
        var services = new ServiceCollection();
        services.AddSingleton(client);
        foreach (var id in new[] { "first", "second", "caught-up" }) {
            services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(id, builder => builder
                .UseCheckpointStore<NoOpCheckpointStore>());
        }
        await using var provider = services.BuildServiceProvider();
        var subscriptions = provider.GetServices<AllStreamSubscription>().ToArray();
        subscriptions.Length.ShouldBe(3);
        subscriptions.Distinct().Count().ShouldBe(3);
        using var metrics = new SubscriptionMetrics(provider.GetServices<GetSubscriptionEndOfStream>());
        // Do not start the subscriptions: advance deterministic checkpoints through the real commit handler.
        await Commit("first", first).NoContext();
        await Commit("second", second).NoContext();
        await Commit("caught-up", tail).NoContext();

        Dictionary<string, long> gaps = new();
        using var listener = new MeterListener {
            InstrumentPublished = (instrument, owner) => {
                if (instrument.Meter.Name == SubscriptionMetrics.MeterName && instrument.Name == SubscriptionMetrics.GapCountMetricName)
                    owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            gaps.Add((string)tags.ToArray().Single(x => x.Key == SubscriptionMetrics.SubscriptionIdTag).Value!, value));
        listener.Start();

        // This client's only Streams/Read calls are the real source-position reads triggered by collection.
        // Appends use a separate client; feature discovery is excluded by the exact gRPC method path.
        requests.Count.ShouldBe(0);
        listener.RecordObservableInstruments();
        gaps.Count.ShouldBe(3);
        gaps["first"].ShouldBe(checked((long)(tail - first)));
        gaps["second"].ShouldBe(checked((long)(tail - second)));
        gaps["caught-up"].ShouldBe(0);
        requests.Count.ShouldBe(1);

        var advanced = await Append().NoContext();
        var newTail = await Append().NoContext();
        newTail.ShouldBeGreaterThan(tail);
        await Commit("second", advanced).NoContext();
        gaps.Clear();
        listener.RecordObservableInstruments();
        gaps.Count.ShouldBe(3);
        gaps["first"].ShouldBe(checked((long)(newTail - first)));
        gaps["second"].ShouldBe(checked((long)(newTail - advanced)));
        gaps["caught-up"].ShouldBe(checked((long)(newTail - tail)));
        requests.Count.ShouldBe(2);

        async Task<ulong> Append() {
            var result = await writer.AppendToStreamAsync(
                stream, StreamState.Any, [new EventData(Uuid.NewUuid(), "shared-gap-event", "{}"u8.ToArray())],
                cancellationToken: cancellationToken
            ).NoContext();
            return result.LogPosition.CommitPosition;
        }

        async Task Commit(string id, ulong position) {
            var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var handler = new CheckpointCommitHandler(id, (checkpoint, _, _) => {
                committed.TrySetResult();
                return new ValueTask<Checkpoint>(checkpoint);
            }, TimeSpan.FromMilliseconds(10));
            (await handler.Commit(new(position, 0, DateTime.UtcNow), cancellationToken).NoContext()).ShouldBeTrue();
            await committed.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).NoContext();
        }
    }

    sealed class TailReadCounter : DelegatingHandler {
        int _count;

        public TailReadCounter() : base(new SocketsHttpHandler()) { }

        public int Count => Volatile.Read(ref _count);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            if (request.RequestUri?.AbsolutePath == "/event_store.client.streams.Streams/Read")
                Interlocked.Increment(ref _count);
            // Count transport attempts, including failures/retries, and forward the actual request unchanged.
            return await base.SendAsync(request, cancellationToken).NoContext();
        }
    }
}
