// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
// ReSharper disable UseDeconstructionOnParameter

namespace Eventuous.Subscriptions.Checkpoints;

public class CommitPositionSequence : SortedSet<CommitPosition> {
    public CommitPositionSequence() : base(new PositionsComparer()) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommitPosition FirstBeforeGap() => Count switch {
        0 => CommitPosition.None,
        1 => Min!,
        _ => Get()
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    CommitPosition Get() {
        var result = this
            .Zip(this.Skip(1), Tuple.Create)
            .FirstOrDefault(tup => tup.Item1.Sequence + 1 != tup.Item2.Sequence);

        return result?.Item1 ?? this.Last();
    }

    class PositionsComparer : IComparer<CommitPosition> {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(CommitPosition x, CommitPosition y) {
            if (x.Sequence == y.Sequence) return 0;

            return x.Sequence > y.Sequence ? 1 : -1;
        }
    }
}