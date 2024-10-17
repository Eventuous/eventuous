using Eventuous.Subscriptions.Checkpoints;
using Eventuous.TestHelpers.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Tests.Subscriptions;

public class SequenceTests {
    public SequenceTests(ITestOutputHelper output) {
        var factory = new LoggerFactory();
        factory.AddProvider(new XUnitLoggerProvider(output));
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(factory);
        var provider = services.BuildServiceProvider();
        provider.AddEventuousLogs();
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void ShouldReturnFirstBefore(CommitPositionSequence sequence, CommitPosition expected) {
        var first = sequence.FirstBeforeGap();
        first.Should().Be(expected);
    }

    [Fact]
    public void ShouldWorkForOne() {
        var timestamp = DateTime.Now;
        var sequence  = new CommitPositionSequence { new(0, 1, timestamp) };
        sequence.FirstBeforeGap().Should().Be(new CommitPosition(0, 1, timestamp));
    }

    [Fact]
    public void ShouldWorkForRandomGap() {
        var random   = new Random();
        var sequence = new CommitPositionSequence();
        var start    = (ulong)random.Next(1);

        for (var i = start; i < start + 100; i++) {
            sequence.Add(new CommitPosition(i, i, DateTime.Now));
        }

        var gapPlace = random.Next(1, sequence.Count - 1);
        sequence.Remove(sequence.ElementAt(gapPlace));
        sequence.Remove(sequence.ElementAt(gapPlace));

        var first = sequence.FirstBeforeGap();
        first.Should().Be(sequence.ElementAt(gapPlace - 1));
    }

    [Fact]
    public void ShouldWorkForNormalCase() {
        var sequence  = new CommitPositionSequence();
        var timestamp = DateTime.Now;

        for (ulong i = 0; i < 10; i++) {
            sequence.Add(new(i, i, timestamp));
        }

        var first = sequence.FirstBeforeGap();
        first.Should().Be(new CommitPosition(9, 9, timestamp));
    }

    public static IEnumerable<object[]> TestData {
        get {
            var timestamp = DateTime.Now;

            object[] sequence1 = [
                new CommitPositionSequence { new(0, 1, timestamp), new(0, 2, timestamp), new(0, 4, timestamp), new(0, 6, timestamp) },
                new CommitPosition(0, 2, timestamp)
            ];

            object[] sequence2 = [
                new CommitPositionSequence { new(0, 1, timestamp), new(0, 2, timestamp), new(0, 8, timestamp), new(0, 6, timestamp) },
                new CommitPosition(0, 2, timestamp)
            ];

            return [sequence1, sequence2];
        }
    }
}
