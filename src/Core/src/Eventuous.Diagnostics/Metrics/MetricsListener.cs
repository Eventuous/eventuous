// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Metrics;

namespace Eventuous.Diagnostics.Metrics;

public sealed class MetricsListener<T> : GenericListener, IDisposable {
    readonly Histogram<double> _duration;
    readonly Counter<long>     _errors;
    readonly Func<T, TagList>  _getTags;

    public MetricsListener(string name, Histogram<double> duration, Counter<long> errors, Func<T, TagList> getTags) : base(name) {
        _duration = duration;
        _errors   = errors;
        _getTags  = getTags;
    }

    protected override void OnEvent(KeyValuePair<string, object?> data) {
        if (data.Value is not MeasureContext { Context: T context } ctx) return;

        var tags = _getTags(context);

        _duration.Record(ctx.Duration.TotalMilliseconds, tags);
        if (ctx.Error) _errors.Add(1, tags);
    }
}

record MeasureContext(TimeSpan Duration, bool Error, object Context);