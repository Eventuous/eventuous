// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Eventuous.Diagnostics.Metrics;

public sealed class MetricsListener<T> : IDisposable {
    readonly Histogram<double> _duration;
    readonly Counter<long>     _errors;
    readonly IDisposable?      _listenerSubscription;
    readonly object            _allListeners = new();

    IDisposable? _networkSubscription;

    public MetricsListener(string name, Histogram<double> duration, Counter<long> errors, Func<T, TagList> getTags) {
        _duration = duration;
        _errors   = errors;

        void WhenHeard(KeyValuePair<string, object?> data) {
            if (data.Value is not MeasureContext { Context: T context } ctx) return;

            var tags = getTags(context);
            
            _duration.Record(ctx.Duration.TotalMilliseconds, tags);
            if (ctx.Error) _errors.Add(1, tags);
        }

        var observer = new Observer<KeyValuePair<string, object?>>(WhenHeard, null);

        void OnNewListener(DiagnosticListener listener) {
            if (listener.Name != name) return;

            lock (_allListeners) {
                _networkSubscription?.Dispose();

                _networkSubscription = listener.Subscribe(observer);
            }
        }

        var newListenerObserver = new Observer<DiagnosticListener>((Action<DiagnosticListener>)OnNewListener, null);

        _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(newListenerObserver);
    }

    public void Dispose() {
        _networkSubscription?.Dispose();
        _listenerSubscription?.Dispose();
    }
}

record MeasureContext(TimeSpan Duration, bool Error, object Context);