// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[PublicAPI]
[Obsolete("Use IEventReader extension functions to load state")]
public class StateStore : IStateStore {
    readonly IEventReader     _eventReader;
    readonly IEventSerializer _serializer;

    const int PageSize = 500;

    public StateStore(IEventReader eventReader, IEventSerializer? serializer = null) {
        _eventReader = Ensure.NotNull(eventReader);
        _serializer  = serializer ?? DefaultEventSerializer.Instance;
    }

    [Obsolete("Use IEventReader.LoadState<T> instead")]
    public async Task<T> LoadState<T>(StreamName stream, CancellationToken cancellationToken)
        where T : State<T>, new() {
        var state = new T();

        const int pageSize = 500;

        var position = StreamReadPosition.Start;

        while (true) {
            var events = await _eventReader.ReadEvents(stream, position, pageSize, cancellationToken).NoContext();

            foreach (var streamEvent in events) {
                Fold(streamEvent);
            }

            if (events.Length < pageSize) break;

            position = new StreamReadPosition(position.Value + events.Length);
        }

        return state;

        void Fold(StreamEvent streamEvent) {
            var evt = streamEvent.Payload;
            if (evt == null) return;

            state = state.When(evt);
        }
    }
}
