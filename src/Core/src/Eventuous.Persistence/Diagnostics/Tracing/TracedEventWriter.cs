// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

using Metrics;
using static Constants;

public class TracedEventWriter(IEventWriter writer) : BaseTracer, IEventWriter {
    public static IEventWriter Trace(IEventWriter writer) => new TracedEventWriter(writer);
    
    readonly string _componentName = writer.GetType().Name;

    public async Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
        ) {
        using var activity = StartActivity(stream, Operations.AppendEvents);

        using var measure = Measure.Start(MetricsSource, new PersistenceMetricsContext(ComponentName, Operations.AppendEvents));

        var tracedEvents = events
            .Select(x => x with { Metadata = x.Metadata.AddActivityTags(activity) })
            .ToArray();

        try {
            var result = await writer.AppendEvents(stream, expectedVersion, tracedEvents, cancellationToken).NoContext();
            activity?.SetActivityStatus(ActivityStatus.Ok());

            return result;
        } catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            measure.SetError();

            throw;
        }
    }

    // ReSharper disable once ConvertToAutoProperty
    protected override string ComponentName => _componentName;
}
