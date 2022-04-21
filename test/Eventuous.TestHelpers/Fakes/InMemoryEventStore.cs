namespace Eventuous.TestHelpers.Fakes;

public class InMemoryEventStore : IEventStore {
    readonly Dictionary<StreamName, InMemoryStream> _storage = new();
    readonly List<StreamEvent>                      _global  = new();

    public Task<bool> StreamExists(StreamName streamName, CancellationToken cancellationToken)
        => Task.FromResult(_storage.ContainsKey(streamName));

    public Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        if (!_storage.TryGetValue(stream, out var existing)) {
            existing         = new InMemoryStream(stream);
            _storage[stream] = existing;
        }

        existing.AppendEvents(expectedVersion, events);

        _global.AddRange(events);

        return Task.FromResult(
            new AppendEventsResult((ulong)(_global.Count - 1), existing.Version)
        );
    }

    public Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    )
        => Task.FromResult(FindStream(stream).GetEvents(start, count).ToArray());

    public Task<StreamEvent[]> ReadEventsBackwards(
        StreamName        stream,
        int               count,
        CancellationToken cancellationToken
    )
        => Task.FromResult(FindStream(stream).GetEventsBackwards(count).ToArray());

    public Task<long> ReadStream(
        StreamName          stream,
        StreamReadPosition  start,
        int                 count,
        Action<StreamEvent> callback,
        CancellationToken   cancellationToken
    ) {
        var readCount = 0L;

        foreach (var streamEvent in FindStream(stream).GetEvents(start, count)) {
            callback(streamEvent);
            readCount++;
        }

        return Task.FromResult(readCount);
    }

    public Task TruncateStream(
        StreamName             stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion  expectedVersion,
        CancellationToken      cancellationToken
    ) {
        FindStream(stream).Truncate(expectedVersion, truncatePosition);
        return Task.CompletedTask;
    }

    public Task DeleteStream(
        StreamName            stream,
        ExpectedStreamVersion expectedVersion,
        CancellationToken     cancellationToken
    ) {
        var existing = FindStream(stream);
        existing.CheckVersion(expectedVersion);
        _storage.Remove(stream);
        return Task.CompletedTask;
    }

    // ReSharper disable once ReturnTypeCanBeEnumerable.Local
    InMemoryStream FindStream(StreamName stream) {
        if (!_storage.TryGetValue(stream, out var existing)) throw new NotFound(stream);

        return existing;
    }

    class NotFound : Exception {
        public NotFound(StreamName stream) : base($"Stream not found: {stream}") { }
    }
}

record StoredEvent(StreamEvent Event, int Position);

class InMemoryStream {
    public int Version { get; private set; } = -1;

    StreamName                 _name;
    readonly List<StoredEvent> _events = new();

    public InMemoryStream(StreamName name)
        => _name = name;

    public void CheckVersion(ExpectedStreamVersion expectedVersion) {
        if (expectedVersion.Value != Version) throw new WrongVersion(expectedVersion, Version);
    }

    public void AppendEvents(
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events
    ) {
        CheckVersion(expectedVersion);

        foreach (var streamEvent in events) {
            _events.Add(new StoredEvent(streamEvent, ++Version));
        }
    }

    public IEnumerable<StreamEvent> GetEvents(StreamReadPosition from, int count) {
        var selected = _events
            .SkipWhile(x => x.Position < from.Value);

        if (count > 0) selected = selected.Take(count);

        return selected.Select(x => x.Event);
    }

    public IEnumerable<StreamEvent> GetEventsBackwards(int count) {
        var position = _events.Count - 1;

        while (count-- > 0) {
            yield return _events[position--].Event;
        }
    }

    public void Truncate(ExpectedStreamVersion version, StreamTruncatePosition position) {
        CheckVersion(version);
        _events.RemoveAll(x => x.Position <= position.Value);
    }
}

class WrongVersion : Exception {
    public WrongVersion(ExpectedStreamVersion expected, int actual)
        : base($"Wrong stream version. Expected {expected.Value}, actual {actual}") { }
}