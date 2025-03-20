using System.Collections.Concurrent;

namespace Eventuous.Testing;

/// <summary>
/// In-memory event store implementation for testing purposes
/// </summary>
public class InMemoryEventStore : IEventStore {
    readonly ConcurrentDictionary<StreamName, InMemoryStream> _storage = new();
    readonly List<StreamEvent>                                _global  = [];

    /// <inheritdoc />
    public Task<bool> StreamExists(StreamName streamName, CancellationToken cancellationToken)
        => Task.FromResult(_storage.ContainsKey(streamName));

    /// <inheritdoc />
    public Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
        ) {
        var existing = _storage.GetOrAdd(stream, s => new(s));
        existing.AppendEvents(expectedVersion, events);
        _global.AddRange(events.Select((x, i) => new StreamEvent(x.Id, x.Payload, x.Metadata, "application/json", _global.Count + i)));

        return Task.FromResult(new AppendEventsResult((ulong)(_global.Count - 1), existing.Version));
    }

    /// <inheritdoc />
    public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken)
        => Task.FromResult(FindStream(stream, failIfNotFound).GetEvents(start, count).ToArray());

    /// <inheritdoc />
    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken)
        => Task.FromResult(FindStream(stream, failIfNotFound).GetEventsBackwards(start, count).ToArray());

    /// <inheritdoc />
    public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        ) {
        FindStream(stream, expectedVersion.ExistingStream).Truncate(expectedVersion, truncatePosition);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken) {
        var existing = FindStream(stream, expectedVersion.ExistingStream);
        existing.CheckVersion(expectedVersion);
        _storage.Remove(stream, out _);

        return Task.CompletedTask;
    }

    // ReSharper disable once ReturnTypeCanBeEnumerable.Local
    InMemoryStream FindStream(StreamName stream, bool failIfNotFound) {
        if (!_storage.TryGetValue(stream, out var existing)) {
            if (failIfNotFound) {
                throw new StreamNotFound(stream);
            }

            return new(stream);
        }

        return existing!;
    }
}

record StoredEvent(StreamEvent Event, int Position);

class InMemoryStream(StreamName name) {
    public int    Version { get; private set; } = -1;
    public string Name    { get; }              = name;

    readonly List<StoredEvent> _events = [];

    public void CheckVersion(ExpectedStreamVersion expectedVersion) {
        if (expectedVersion != ExpectedStreamVersion.Any && expectedVersion.Value != Version) throw new WrongVersion(expectedVersion, Version);
    }

    public void AppendEvents(ExpectedStreamVersion expectedVersion, IReadOnlyCollection<NewStreamEvent> events) {
        CheckVersion(expectedVersion);

        foreach (var newEvent in events) {
            var version     = ++Version;
            var streamEvent = new StreamEvent(newEvent.Id, newEvent.Payload, newEvent.Metadata, "application/json", version);
            _events.Add(new(streamEvent, version));
        }
    }

    public IEnumerable<StreamEvent> GetEvents(StreamReadPosition from, int count) {
        var selected = _events.SkipWhile(x => x.Position < from.Value);

        if (count > 0) selected = selected.Take(count);

        return selected.Select(x => x.Event with { Position = x.Position });
    }

    public IEnumerable<StreamEvent> GetEventsBackwards(StreamReadPosition from, int count) {
        var position = (int)from.Value;

        while (count-- > 0) {
            yield return _events[position--].Event;
        }
    }

    public void Truncate(ExpectedStreamVersion version, StreamTruncatePosition position) {
        CheckVersion(version);
        _events.RemoveAll(x => x.Position <= position.Value);
    }
}

public class WrongVersion(ExpectedStreamVersion expected, int actual) : Exception($"Wrong stream version. Expected {expected.Value}, actual {actual}");
