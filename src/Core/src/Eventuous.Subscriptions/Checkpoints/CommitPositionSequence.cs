using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions.Checkpoints;

public class CommitPositionSequence : SortedSet<CommitPosition> {
    public CommitPositionSequence() : base(new PositionsComparer()) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommitPosition FirstBeforeGap() => Count switch {
        0 => CommitPosition.None,
        1 => Min!,
        _ => this
            .Zip(this.Skip(1), Tuple.Create)
            .First(tup => tup.Item1.Sequence + 1 != tup.Item2.Sequence).Item1
    };

    class PositionsComparer : IComparer<CommitPosition> {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(CommitPosition? x, CommitPosition? y) {
            if (x == null || y == null || x.Sequence == y.Sequence) return 0;

            return x.Sequence > y.Sequence ? 1 : -1;
        }
    }
}