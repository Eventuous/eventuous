namespace Eventuous.Tests.AspNetCore.Sut;

public class FakeStore : IEventStore {
    public Task<bool> StreamExists(StreamName streamName, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<AppendEventsResult> AppendEvents(
            StreamName                       stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events,
            CancellationToken                cancellationToken
        ) => default!;

    public Task<StreamEvent[]> ReadEvents(
            StreamName         stream,
            StreamReadPosition start,
            int                count,
            CancellationToken  cancellationToken
        ) => default!;

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken) => default!;

    public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        ) => default!;

    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken) => default!;
}
