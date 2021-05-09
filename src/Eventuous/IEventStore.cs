using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Eventuous {
    public interface IEventStore {
        Task AppendEvents(string stream, ExpectedStreamVersion expectedVersion, IReadOnlyCollection<StreamEvent> events, CancellationToken cancellationToken);
        
        Task<StreamEvent[]> ReadEvents(string stream, StreamReadPosition start, int count, CancellationToken cancellationToken);
        
        Task<StreamEvent[]> ReadEventsBackwards(string stream, int count, CancellationToken cancellationToken);
        
        Task ReadStream(string stream, StreamReadPosition start, Action<StreamEvent> callback, CancellationToken cancellationToken);
    }
}