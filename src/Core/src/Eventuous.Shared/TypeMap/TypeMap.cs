// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Eventuous;

public class TypeMap<TV> : ITypeMap<TV> {
    private MapEntry[] _values;

    public TypeMap() => _values = new MapEntry[ITypeMap<TV>.RecommendedCapacity];

    public ITypeMap<TV> Add<TK>(TV value) {
        if (!TryAdd(ITypeMap<TV>.GetIndex<TK>(), value))
            throw new Exceptions.DuplicateTypeException<TK>();

        return this;
    }

    public bool TryGetValue<TK>([MaybeNullWhen(false)] out TV value) {
        var index = ITypeMap<TV>.GetIndex<TK>();

        if ((uint)index >= (uint)_values.Length) {
            value = default;
            return false;
        }

        ref var entry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), index);
        value = entry.Value;
        return entry.HasValue;
    }

    void EnsureCapacity(int index) {
        if ((uint)index >= (uint)_values.Length) Array.Resize(ref _values, ITypeMap<TV>.RecommendedCapacity);
    }

    bool TryAdd(int index, TV value) {
        EnsureCapacity(index);
        ref var entry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), index);
        if (entry.HasValue) return false;

        entry.Value    = value;
        entry.HasValue = true;
        return true;
    }

    struct MapEntry {
        public bool HasValue { get; set; }
        public TV?  Value    { get; set; }
    }
}
