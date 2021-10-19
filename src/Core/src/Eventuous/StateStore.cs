namespace Eventuous; 

[PublicAPI]
public class StateStore : IStateStore {
    readonly IEventStore      _eventStore;
    readonly IEventSerializer _serializer;

    const int PageSize = 500;

    public StateStore(IEventStore eventStore, IEventSerializer? serializer = null) {
        _eventStore = Ensure.NotNull(eventStore, nameof(eventStore));
        _serializer = serializer ?? DefaultEventSerializer.Instance;
    }

    public async Task<T> LoadState<T, TId>(StreamName stream, CancellationToken cancellationToken)
        where T : AggregateState<T, TId>, new() where TId : AggregateId {
        var state = new T();

        var position = StreamReadPosition.Start;

        while (true) {
            var readCount = await _eventStore.ReadStream(stream, position, PageSize, Fold, cancellationToken).NoContext();
            if (readCount == 0) break;

            position = new StreamReadPosition(position.Value + readCount);
        }

        return state;

        void Fold(StreamEvent streamEvent) {
            var evt = Deserialize(streamEvent);
            if (evt == null) return;

            state = state.When(evt);
        }

        object? Deserialize(StreamEvent streamEvent)
            => _serializer.DeserializeEvent(streamEvent.Data.AsSpan(), streamEvent.EventType);
    }
}