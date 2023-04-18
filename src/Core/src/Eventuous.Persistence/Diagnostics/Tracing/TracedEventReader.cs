// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

using static Constants;

public class TracedEventReader : BaseTracer, IEventReader {
    public static IEventReader Trace(IEventReader reader)
        => new TracedEventReader(reader);

    public TracedEventReader(IEventReader reader)
        => Inner = reader;

    IEventReader Inner { get; }

    public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
        => Trace(stream, Operations.ReadEvents, () => Inner.ReadEvents(stream, start, count, cancellationToken));
}
