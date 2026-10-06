using System.Runtime.CompilerServices;
using Eventuous.Testing;
using Shouldly;

namespace Eventuous.Tests.Application;

public class TieredEventStoreTests {
    static readonly StreamName Stream = new("tiered-test");

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Stream_exists_when_either_tier_contains_it(bool inHot, bool inArchive) {
        var hot = new InMemoryEventStore();
        var archive = new InMemoryEventStore();
        if (inHot) await Seed(hot);
        if (inArchive) await Seed(archive);

        (await new TieredEventStore(hot, archive).StreamExists(Stream)).ShouldBe(inHot || inArchive);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Existence_can_be_determined_from_a_read_only_archive(bool present) {
        var archive = new InMemoryEventStore();
        if (present) await Seed(archive);
        var store = new TieredEventStore(new InMemoryEventStore(), new ReaderOnly(archive));
        (await store.StreamExists(Stream)).ShouldBe(present);
    }

    [Test]
    public async Task Empty_existing_archive_stream_still_exists() {
        var archive = new InMemoryEventStore();
        await Seed(archive);
        await archive.TruncateStream(Stream, new(2), ExpectedStreamVersion.Any, default);
        var store = new TieredEventStore(new InMemoryEventStore(), new ReaderOnly(archive));
        (await store.StreamExists(Stream)).ShouldBeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Archive_read_failure_is_not_reported_as_a_missing_stream(bool cancelled) {
        using var cancellation = new CancellationTokenSource();
        if (cancelled) cancellation.Cancel();
        Exception failure = cancelled ? new OperationCanceledException(cancellation.Token) : new IOException("Archive unavailable");
        var archive = new FailingReader(failure);
        var store = new TieredEventStore(new InMemoryEventStore(), archive);

        var thrown = await Should.ThrowAsync<Exception>(() => store.StreamExists(Stream, cancellation.Token));

        if (cancelled) thrown.ShouldBeAssignableTo<OperationCanceledException>();
        else thrown.ShouldBeSameAs(failure);
        archive.Token.ShouldBe(cancellation.Token);
        archive.Disposed.ShouldBeTrue();
    }

    [Test]
    public async Task Hot_stream_exists_even_when_archive_is_unavailable() {
        var hot = new InMemoryEventStore();
        await Seed(hot);
        var archive = new FailingReader(new IOException("Archive unavailable"));

        (await new TieredEventStore(hot, archive).StreamExists(Stream)).ShouldBeTrue();
        archive.Disposed.ShouldBeFalse();
    }

    [Test]
    public async Task Reads_span_tiers_in_both_directions_without_duplicates() {
        var events = Enumerable.Range(0, 6)
            .Select(i => new StreamEvent(Guid.NewGuid(), i, new(), "application/json", i, DateTime.UtcNow)).ToArray();
        // The archive overlaps the hot window at revision 3.
        var store = new TieredEventStore(new ReadWindow(events[3..]), new ReadWindow(events[..4]));
        var forward = await store.ReadEvents(Stream, new(1), 4, true, default);
        var backward = await store.ReadEventsBackwards(Stream, new(5), 4, true, default);
        forward.Select(x => x.Revision).ShouldBe(new long[] { 1, 2, 3, 4 });
        backward.Select(x => x.Revision).ShouldBe(new long[] { 5, 4, 3, 2 });
        forward.Select(x => x.Payload).ShouldBe(new object[] { 1, 2, 3, 4 });
        backward.Select(x => x.Payload).ShouldBe(new object[] { 5, 4, 3, 2 });
        forward.Select(x => x.FromArchive).ShouldBe(new[] { true, true, false, false });
        backward.Select(x => x.FromArchive).ShouldBe(new[] { false, false, false, true });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Appends_only_modify_the_hot_store(bool batch) {
        var hot = new InMemoryEventStore();
        var archive = new InMemoryEventStore();
        await Seed(archive);
        var store = new TieredEventStore(hot, archive);
        var events = Events(10, 11);
        if (batch) {
            var results = await store.AppendEvents(new[] { new NewStreamAppend(Stream, ExpectedStreamVersion.NoStream, events) }, default);
            results.Single().NextExpectedVersion.ShouldBe(1);
        }
        else {
            var result = await store.AppendEvents(Stream, ExpectedStreamVersion.NoStream, events, default);
            result.NextExpectedVersion.ShouldBe(1);
        }
        (await Read(hot)).ShouldBe(new[] { 10, 11 });
        (await Read(archive)).ShouldBe(new[] { 0, 1, 2 });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Destructive_operations_leave_archive_events_intact(bool delete) {
        var hot = new InMemoryEventStore();
        var archive = new InMemoryEventStore();
        await Seed(hot);
        await Seed(archive);
        var store = new TieredEventStore(hot, archive);
        if (delete) {
            await store.DeleteStream(Stream, new(2));
            (await hot.StreamExists(Stream, default)).ShouldBeFalse();
        }
        else {
            await store.TruncateStream(Stream, new(0), new(2));
            (await Read(hot)).ShouldBe(new[] { 1, 2 });
        }
        (await Read(archive)).ShouldBe(new[] { 0, 1, 2 });
        (await store.StreamExists(Stream)).ShouldBeTrue();
    }

    static NewStreamEvent[] Events(params int[] values) => values.Select(x => new NewStreamEvent(Guid.NewGuid(), x, new())).ToArray();
    static Task<AppendEventsResult> Seed(IEventStore store) => store.AppendEvents(Stream, ExpectedStreamVersion.NoStream, Events(0, 1, 2), default);
    static async Task<int[]> Read(IEventReader reader) => (await reader.ReadEvents(Stream, StreamReadPosition.Start, 10, true, default)).Select(x => (int)x.Payload!).ToArray();

    sealed class ReaderOnly(IEventReader inner) : IEventReader {
        public IAsyncEnumerable<StreamEvent> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
            => inner.ReadEvents(stream, start, count, cancellationToken);
        public IAsyncEnumerable<StreamEvent> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
            => inner.ReadEventsBackwards(stream, start, count, cancellationToken);
    }

    sealed class FailingReader(Exception failure) : IEventReader {
        public CancellationToken Token { get; private set; }
        public bool Disposed { get; private set; }

        public async IAsyncEnumerable<StreamEvent> ReadEvents(StreamName stream, StreamReadPosition start, int count, [EnumeratorCancellation] CancellationToken cancellationToken) {
            Token = cancellationToken;
            try { await Task.FromException(failure); }
            finally { Disposed = true; }
            yield break;
        }

        public IAsyncEnumerable<StreamEvent> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    // Controlled read windows retain original revisions after truncation, just like provider stores.
    sealed class ReadWindow(StreamEvent[] events) : InMemoryEventStore, IEventReader {
        IAsyncEnumerable<StreamEvent> IEventReader.ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
            => Yield(events.Where(x => x.Revision >= start.Value).Take(count), cancellationToken);
        IAsyncEnumerable<StreamEvent> IEventReader.ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
            => Yield(events.Where(x => x.Revision <= start.Value).Reverse().Take(count), cancellationToken);

        static async IAsyncEnumerable<StreamEvent> Yield(IEnumerable<StreamEvent> events, [EnumeratorCancellation] CancellationToken ct) {
            foreach (var evt in events) {
                ct.ThrowIfCancellationRequested();
                yield return evt;
            }
            await Task.CompletedTask;
        }
    }
}
