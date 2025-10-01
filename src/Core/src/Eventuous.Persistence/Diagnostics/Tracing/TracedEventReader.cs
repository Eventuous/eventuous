// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

using static Constants;

public class TracedEventReader(IEventReader reader) : BaseTracer, IEventReader {
    public static IEventReader Trace(IEventReader reader) => new TracedEventReader(reader);

    readonly string _componentName = reader.GetType().Name;

    IEventReader Inner { get; } = reader;

    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken)
        => Trace(stream, Operations.ReadEvents, () => Inner.ReadEvents(stream, start, count, failIfNotFound, cancellationToken));

    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken)
        => Trace(stream, Operations.ReadEvents, () => Inner.ReadEventsBackwards(stream, start, count, failIfNotFound, cancellationToken));

    // ReSharper disable once ConvertToAutoProperty
    protected override string ComponentName => _componentName;
}
