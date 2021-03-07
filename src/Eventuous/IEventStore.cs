using System.Collections.Generic;
using System.Threading.Tasks;

namespace Eventuous {
    public interface IEventStore {
        Task AppendEvents(string stream, ExpectedStreamVersion expectedVersion, IReadOnlyCollection<StreamEvent> events);
        
        Task<StreamEvent[]> ReadEvents(string stream, StreamReadPosition start);
    }
}