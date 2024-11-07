// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

// ReSharper disable UseDeconstructionOnParameter

namespace Eventuous.Subscriptions.Checkpoints;

using Diagnostics;

public class CommitPositionSequence() : SortedSet<CommitPosition>(new PositionsComparer()) {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommitPosition FirstBeforeGap()
        => Count switch {
            0 => CommitPosition.None,
            1 => Min,
            _ => Get()
        };

    CommitPosition Get() {
        var result = this
            .Zip(this.Skip(1), Tuple.Create)
            .FirstOrDefault(tup => tup.Item1.Sequence + 1 != tup.Item2.Sequence);

        if (result == null) return Max;

        SubscriptionsEventSource.Log.CheckpointGapDetected(result.Item1, result.Item2);
        return result.Item1;
    }

    class PositionsComparer : IComparer<CommitPosition> {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(CommitPosition x, CommitPosition y) {
            if (x.Sequence == y.Sequence) return 0;

            return x.Sequence > y.Sequence ? 1 : -1;
        }
    }
}
