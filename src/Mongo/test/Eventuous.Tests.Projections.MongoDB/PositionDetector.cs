using EventStore.Client;

namespace Eventuous.Tests.Projections.MongoDB; 

public static class PositionDetector {
    public static async Task<ulong?> GetPositionToSubscribe(this EventStoreClient client, string stream) {
        ulong? startPosition = null;

        try {
            var last = await client.ReadStreamAsync(
                Direction.Backwards,
                stream,
                StreamPosition.End,
                1
            ).ToArrayAsync();

            startPosition = last[0].OriginalEventNumber;
        }
        catch (StreamNotFound) { }

        return startPosition;
    }
}