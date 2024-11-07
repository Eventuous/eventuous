// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics.Metrics;

public sealed class Measure(DiagnosticSource diagnosticSource, object context) : IDisposable {
    public static Measure Start(DiagnosticSource source, object context) => new(source, context);

    public const string EventName = "Stopped";

    public void SetError() => _error = true;

    void Record() {
        var stoppedAt = DateTime.UtcNow;
        var duration  = stoppedAt - _startedAt;
        diagnosticSource.Write(EventName, new MeasureContext(duration, _error, context));
    }

    readonly DateTime _startedAt = DateTime.UtcNow;

    bool _error;

    public void Dispose() => Record();
}
