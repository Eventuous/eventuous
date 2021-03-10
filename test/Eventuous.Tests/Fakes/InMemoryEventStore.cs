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

        public Task<StreamEvent[]> ReadEvents(string stream, StreamReadPosition start) {
            if (!_storage.TryGetValue(stream, out var existing))
                throw new NotFound(stream);

            return Task.FromResult(existing.ToArray());
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