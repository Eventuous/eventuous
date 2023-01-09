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

        var iobserver = new Observer<KeyValuePair<string, object?>>(WhenHeard, null);

        void OnNewListener(DiagnosticListener listener) {
            if (listener.Name != name) return;

            lock (_allListeners) {
                _networkSubscription?.Dispose();

                _networkSubscription = listener.Subscribe(iobserver);
            }
        }

        var observer = new Observer<DiagnosticListener>((Action<DiagnosticListener>)OnNewListener, null);

        _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(observer);
    }

    public void Dispose() {
        _networkSubscription?.Dispose();
        _listenerSubscription?.Dispose();
    }
}

class Observer<T> : IObserver<T> {
    public Observer(Action<T>? onNext, Action? onCompleted) {
        _onNext      = onNext ?? new Action<T>(_ => { });
        _onCompleted = onCompleted ?? new Action(() => { });
    }

    public void OnCompleted() => _onCompleted();

    public void OnError(Exception error) { }

    public void OnNext(T value) => _onNext(value);

    readonly Action<T> _onNext;
    readonly Action    _onCompleted;
}

record MeasureContext(TimeSpan Duration, bool Error, object Context);