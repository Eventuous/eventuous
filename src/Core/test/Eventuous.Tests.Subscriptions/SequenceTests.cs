using Eventuous.Subscriptions.Checkpoints;

namespace Eventuous.Tests.Subscriptions;

public class SequenceTests {
    [Theory]
    [MemberData(nameof(TestData))]
    public void ShouldReturnFirstBefore(CommitPositionSequence sequence, CommitPosition expected) {
        var first = sequence.FirstBeforeGap();
        first.Should().Be(expected);
    }

    [Fact]
    public void ShouldWorkForOne() {
        var sequence = new CommitPositionSequence { new(0, 1) };
        sequence.FirstBeforeGap().Should().Be(new CommitPosition(0, 1));
    }

    [Fact]
    public void ShouldWorkForRandomGap() {
        var random   = new Random();
        var sequence = new CommitPositionSequence();
        var start    = (ulong)random.Next(1);

        for (var i = start; i < start + 100; i++) {
            sequence.Add(new CommitPosition(i, i));
        }

        var gapPlace = random.Next(1, sequence.Count - 1);
        sequence.Remove(sequence.ElementAt(gapPlace));
        sequence.Remove(sequence.ElementAt(gapPlace));

        var first = sequence.FirstBeforeGap();
        first.Should().Be(sequence.ElementAt(gapPlace - 1));
    }

    [Fact]
    public void ShouldWorkForNormalCase() {
        var sequence = new CommitPositionSequence();

        for (ulong i = 0; i < 10; i++) {
            sequence.Add(new CommitPosition(i, i));
        }

        var first = sequence.FirstBeforeGap();
        first.Should().Be(new CommitPosition(9, 9));
    }

    public static IEnumerable<object[]> TestData =>
        new List<object[]>
        {
            new object[] {new CommitPositionSequence { new(0, 1), new(0, 2), new(0, 4), new(0, 6) }, new CommitPosition(0, 2)},
            new object[] {new CommitPositionSequence { new(0, 1), new(0, 2), new(0, 8), new(0, 6) }, new CommitPosition(0, 2)}
        };
}