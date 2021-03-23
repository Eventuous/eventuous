using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eventuous.Tests.Fakes {
    public class InMemoryEventStore : IEventStore {
        readonly Dictionary<string, List<StreamEvent>> _storage = new();

        public Task AppendEvents(
            string stream, ExpectedStreamVersion expectedVersion, IReadOnlyCollection<StreamEvent> events
        ) {
            return _storage.TryGetValue(stream, out var existing) ? AddToExisting() : AddToNew();

            Task AddToExisting() {
                if (existing.Count >= expectedVersion.Value)
                    throw new WrongVersion(expectedVersion, existing.Count - 1);

                existing.AddRange(events);
                return Task.CompletedTask;
            }

            Task AddToNew() {
                _storage[stream] = events.ToList();
                return Task.CompletedTask;
            }
        }

        public Task<StreamEvent[]> ReadEvents(string stream, StreamReadPosition start, int count)
            => Task.FromResult(FindStream(stream).Take(count).ToArray());

        public Task<StreamEvent[]> ReadEventsBackwards(string stream, int count) {
            var reversed = new List<StreamEvent>(FindStream(stream));
            reversed.Reverse();

            return Task.FromResult(reversed.Take(count).ToArray());
        }

        public Task ReadStream(string stream, StreamReadPosition start, Action<StreamEvent> callback) {
            foreach (var streamEvent in FindStream(stream)) {
                callback(streamEvent);
            }
            return Task.CompletedTask;
        }

        // ReSharper disable once ReturnTypeCanBeEnumerable.Local
        List<StreamEvent> FindStream(string stream) {
            if (!_storage.TryGetValue(stream, out var existing))
                throw new NotFound(stream);

            return existing;
        }

        class WrongVersion : Exception {
            public WrongVersion(ExpectedStreamVersion expected, int actual)
                : base($"Wrong stream version. Expected {expected.Value}, actual {actual}") { }
        }

        class NotFound : Exception {
            public NotFound(string stream) : base($"Stream not found: {stream}") { }
        }
    }
}