// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous;

[PublicAPI]
[Obsolete("Use IEventReader extension functions to load state")]
public class StateStore(IEventReader eventReader, IEventSerializer? serializer = null) : IStateStore {
    readonly IEventReader     _eventReader = Ensure.NotNull(eventReader);
    readonly IEventSerializer _serializer  = serializer ?? DefaultEventSerializer.Instance;

    const int PageSize = 500;

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

            position = new(position.Value + events.Length);
        }

        return state;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Fold(StreamEvent streamEvent) {
            var evt = streamEvent.Payload;

            if (evt == null) return;

            state = state.When(evt);
        }
    }
}
