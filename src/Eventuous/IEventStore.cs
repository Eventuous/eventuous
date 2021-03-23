using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Eventuous {
    public interface IEventStore {
        Task AppendEvents(string stream, ExpectedStreamVersion expectedVersion, IReadOnlyCollection<StreamEvent> events);
        
        Task<StreamEvent[]> ReadEvents(string stream, StreamReadPosition start, int count);
        
        Task<StreamEvent[]> ReadEventsBackwards(string stream, int count);
        
        Task ReadStream(string stream, StreamReadPosition start, Action<StreamEvent> callback);
    }
}