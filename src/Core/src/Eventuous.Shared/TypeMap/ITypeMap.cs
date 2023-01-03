// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;

namespace Eventuous.TypeMap;

public interface IReadOnlyTypeMap<TV> {
    bool TryGetValue<TK>([MaybeNullWhen(false)] out TV value);
}

public interface ITypeMap<TV> : IReadOnlyTypeMap<TV> {
    // ReSharper disable once StaticMemberInGenericType
    private static volatile int index = -1;

    private const int DefaultCapacity = 10;

    private static class TypeSlot<T> {
        // ReSharper disable once StaticMemberInGenericType
        internal static readonly int Index = Interlocked.Increment(ref index);
    }

    private protected static int GetIndex<TK>() => TypeSlot<TK>.Index;

    private protected static int RecommendedCapacity {
        get {
            var capacity = index + 1;

            if (capacity < DefaultCapacity) {
                capacity = DefaultCapacity;
            }
            else {
                capacity += DefaultCapacity;
                if ((uint)capacity > Array.MaxLength) capacity = Array.MaxLength;
            }

            return capacity;
        }
    }

    ITypeMap<TV> Add<TKey>(TV value);
}
