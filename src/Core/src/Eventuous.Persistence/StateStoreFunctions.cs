// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.Diagnostics.PersistenceEventSource;

namespace Eventuous;

public record FoldedEventStream<T> where T : State<T>, new() {
    public FoldedEventStream(
        StreamName            streamName,
        ExpectedStreamVersion streamVersion,
        object[]              events
    ) {
        StreamName    = streamName;
        StreamVersion = streamVersion;
        Events        = events;
        State         = events.Aggregate(new T(), (state, o) => state.When(o));
    }

    public StreamName            StreamName    { get; }
    public ExpectedStreamVersion StreamVersion { get; }
    public object[]              Events        { get; }
    public T                     State         { get; }

    public void Deconstruct(
        out StreamName            streamName,
        out ExpectedStreamVersion streamVersion,
        out object[]              events
    ) {
        streamName    = StreamName;
        streamVersion = StreamVersion;
        events        = Events;
    }
}

public static class StateStoreFunctions {
    public static Task<FoldedEventStream<T>> LoadState<T>(this IEventReader reader, StreamName streamName, CancellationToken cancellationToken)
        where T : State<T>, new() {
        return reader.LoadEventsInternal<T>(streamName, true, cancellationToken);
    }

    public static Task<FoldedEventStream<T>> LoadStateOrNew<T>(this IEventReader reader, StreamName streamName, CancellationToken cancellationToken)
        where T : State<T>, new() {
        return reader.LoadEventsInternal<T>(streamName, false, cancellationToken);
    }

    static async Task<FoldedEventStream<T>> LoadEventsInternal<T>(
        this IEventReader reader,
        StreamName        streamName,
        bool              failIfNotFound,
        CancellationToken cancellationToken
    ) where T : State<T>, new() {
        try {
            var streamEvents = await reader.ReadStream(
                streamName,
                StreamReadPosition.Start,
                failIfNotFound,
                cancellationToken
            );

            var events = streamEvents.Select(x => x.Payload!).ToArray();
            return (new FoldedEventStream<T>(streamName, new ExpectedStreamVersion(streamEvents.Last().Position), events));
        }
        catch (StreamNotFound) when (!failIfNotFound) {
            return new FoldedEventStream<T>(streamName, ExpectedStreamVersion.NoStream, Array.Empty<object>());
        }
        catch (Exception e) {
            Log.UnableToLoadStream(streamName, e);
            throw;
        }
    }
}
