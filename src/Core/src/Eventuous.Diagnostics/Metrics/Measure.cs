// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Eventuous.Diagnostics.Metrics;

public sealed class Measure : IDisposable {
    readonly DiagnosticSource _diagnosticSource;
    readonly object           _context;

    public static Measure Start(DiagnosticSource source, object context) => new(source, context);

    Measure(DiagnosticSource diagnosticSource, object context) {
        _diagnosticSource = diagnosticSource;
        _context          = context;
    }

    public void SetError() => _error = true;

    void Record() {
        var stoppedAt = DateTime.UtcNow;
        var duration = stoppedAt - _startedAt;
        _diagnosticSource.Write("Stopped", new MeasureContext(duration, _error, _context));
    }

    readonly DateTime _startedAt = DateTime.UtcNow;

    bool _error;

    public void Dispose() => Record();
}