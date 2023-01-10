// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics.Metrics;

public class GenericObserver<T> : IObserver<T> {
    public GenericObserver(Action<T>? onNext, Action? onCompleted = null) {
        _onNext      = onNext ?? new Action<T>(_ => { });
        _onCompleted = onCompleted ?? new Action(() => { });
    }

    public void OnCompleted() => _onCompleted();

    public void OnError(Exception error) { }

    public void OnNext(T value) => _onNext(value);

    readonly Action<T> _onNext;
    readonly Action    _onCompleted;
}