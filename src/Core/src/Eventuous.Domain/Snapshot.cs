// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public record Snapshot(long Version);

public record Snapshot<T> : Snapshot {
    public T State { get; init; }
    public Snapshot(T state, long version) : base(version) {
        State = state;
    }
}
