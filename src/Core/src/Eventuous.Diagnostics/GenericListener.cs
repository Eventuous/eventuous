// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

using Metrics;

public abstract class GenericListener {
    readonly IDisposable? _listenerSubscription;
    readonly object       _allListeners = new();

    IDisposable? _networkSubscription;

    protected GenericListener(string name) {
        var observer = new GenericObserver<KeyValuePair<string, object?>>(OnEvent);

        var newListenerObserver = new GenericObserver<DiagnosticListener>((Action<DiagnosticListener>)OnNewListener);

        _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(newListenerObserver);

        return;

        void OnNewListener(DiagnosticListener listener) {
            if (listener.Name != name) return;

            lock (_allListeners) {
                _networkSubscription?.Dispose();

                _networkSubscription = listener.Subscribe(observer);
            }
        }
    }

    protected abstract void OnEvent(KeyValuePair<string, object?> obj);

    public void Dispose() {
        _networkSubscription?.Dispose();
        _listenerSubscription?.Dispose();
    }
}
