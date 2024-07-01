using Eventuous.Testing;

namespace Eventuous.Tests.Persistence;

public class TieredStoreTests {
    readonly InMemoryEventStore _hotStore  = new();
    readonly InMemoryEventStore _coldStore = new();
    
    // [Fact]
    // public async Task 
}
