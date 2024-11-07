// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

using Metrics;

public abstract class GenericListener : IObserver<KeyValuePair<string, object?>> {
    readonly IDisposable? _listenerSubscription;
    readonly object       _allListeners = new();

    IDisposable? _subscription;

    protected GenericListener(string name) {
        var observer = this;

        var newListenerObserver = new GenericObserver<DiagnosticListener>((Action<DiagnosticListener>)OnNewListener);

        _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(newListenerObserver);

        return;

        void OnNewListener(DiagnosticListener listener) {
            if (listener.Name != name) return;

            lock (_allListeners) {
                _subscription?.Dispose();
                _subscription = listener.Subscribe(observer);
            }
        }
    }

    protected abstract void OnEvent(KeyValuePair<string, object?> obj);

    public void Dispose() {
        _subscription?.Dispose();
        _listenerSubscription?.Dispose();
    }

    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }

    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error) { }

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> value) => OnEvent(value);
}
