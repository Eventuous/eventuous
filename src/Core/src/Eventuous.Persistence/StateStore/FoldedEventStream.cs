// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public record FoldedEventStream<T> where T : State<T>, new() {
    public FoldedEventStream(StreamName streamName, ExpectedStreamVersion streamVersion, object[] events) {
        StreamName    = streamName;
        StreamVersion = streamVersion;
        Events        = events;
        State         = events.Aggregate(new T(), (state, o) => state.When(o));
    }

    public StreamName            StreamName    { get; }
    public ExpectedStreamVersion StreamVersion { get; }
    public object[]              Events        { get; }
    public T                     State         { get; init; }
}
