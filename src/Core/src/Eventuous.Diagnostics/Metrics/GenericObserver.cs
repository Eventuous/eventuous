// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics.Metrics;

class GenericObserver<T>(Action<T>? onNext, Action? onCompleted = null) : IObserver<T> {
    public void OnCompleted() => _onCompleted();

    public void OnError(Exception error) { }

    public void OnNext(T value) => _onNext(value);

    readonly Action<T> _onNext      = onNext      ?? (_ => { });
    readonly Action    _onCompleted = onCompleted ?? (() => { });
}