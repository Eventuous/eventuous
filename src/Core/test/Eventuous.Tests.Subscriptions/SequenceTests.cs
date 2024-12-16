using Eventuous.Subscriptions.Checkpoints;
using Eventuous.TestHelpers.TUnit.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Tests.Subscriptions;

public class SequenceTests {
    public SequenceTests() {
        var factory = new LoggerFactory();
        factory.AddProvider(new TUnitLoggerProvider());
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(factory);
        var provider = services.BuildServiceProvider();
        provider.AddEventuousLogs();
    }

    [Test]
    [MethodDataSource(nameof(TestData))]
    public void ShouldReturnFirstBefore(CommitPositionSequence sequence, CommitPosition expected) {
        var first = sequence.FirstBeforeGap();
        first.Should().Be(expected);
    }

    [Test]
    public void ShouldWorkForOne() {
        var timestamp = DateTime.Now;
        var sequence  = new CommitPositionSequence { new(0, 1, timestamp) };
        sequence.FirstBeforeGap().Should().Be(new CommitPosition(0, 1, timestamp));
    }

    [Test]
    public void ShouldWorkForRandomGap() {
        var random   = new Random();
        var sequence = new CommitPositionSequence();
        var start    = (ulong)random.Next(1);

        for (var i = start; i < start + 100; i++) {
            sequence.Add(new(i, i, DateTime.Now));
        }

        var gapPlace = random.Next(1, sequence.Count - 1);
        sequence.Remove(sequence.ElementAt(gapPlace));
        sequence.Remove(sequence.ElementAt(gapPlace));

        var first = sequence.FirstBeforeGap();
        first.Should().Be(sequence.ElementAt(gapPlace - 1));
    }

    [Test]
    public void ShouldWorkForNormalCase() {
        var sequence  = new CommitPositionSequence();
        var timestamp = DateTime.Now;

        for (ulong i = 0; i < 10; i++) {
            sequence.Add(new(i, i, timestamp));
        }

        var first = sequence.FirstBeforeGap();
        first.Should().Be(new CommitPosition(9, 9, timestamp));
    }

    public static IEnumerable<Func<(CommitPositionSequence, CommitPosition)>> TestData() {
        var timestamp = DateTime.Now;

        yield return () => ([new(0, 1, timestamp), new(0, 2, timestamp), new(0, 4, timestamp), new(0, 6, timestamp)], new(0, 2, timestamp));
        yield return () => ([new(0, 1, timestamp), new(0, 2, timestamp), new(0, 8, timestamp), new(0, 6, timestamp)], new(0, 2, timestamp));
    }
}
