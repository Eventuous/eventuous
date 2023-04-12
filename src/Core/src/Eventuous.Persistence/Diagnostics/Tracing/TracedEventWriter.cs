// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

using Metrics;
using static Constants;

public class TracedEventWriter : BaseTracer, IEventWriter {
    public static IEventWriter Trace(IEventWriter writer)
        => new TracedEventWriter(writer);

    public TracedEventWriter(IEventWriter writer)
        => Inner = writer;

    IEventWriter Inner { get; }

    public async Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        using var activity = StartActivity(stream, Operations.AppendEvents);

        using var measure = Measure.Start(
            _metricsSource,
            new EventStoreMetricsContext(Operations.AppendEvents)
        );

        var tracedEvents = events.Select(
                x => x with { Metadata = x.Metadata.AddActivityTags(activity) }
            )
            .ToArray();

        try {
            var result = await Inner.AppendEvents(stream, expectedVersion, tracedEvents, cancellationToken).NoContext();
            activity?.SetActivityStatus(ActivityStatus.Ok());
            return result;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            measure.SetError();
            throw;
        }
    }
}
