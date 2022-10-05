// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions.Filters.Partitioning;

static class MurmurHash3 {
    const uint C1 = 0xCC9E2D51U;
    const uint C2 = 0x1B873593U;
    
    const uint Seed = 0xc58f1a7b;

    const int CharSize = sizeof(char);

    static unsafe Span<byte> GetBytes(string data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) return Span<byte>.Empty;

        fixed (char* p = data)
        {
            return new Span<byte>(p, data.Length * CharSize);
        }
    }

    public static uint Hash(string partitionKey) {
        var bytes    = GetBytes(partitionKey);
        var length   = bytes.Length;
        var h1       = Seed;
        var tailLen  = length & 3;
        var position = length - tailLen;

        for (var start = 0; start < position; start += 4) {
            var k1l = BitConverter.ToUInt32(bytes.Slice(start, 4));
            k1l *= C1;
            k1l =  Rl(k1l, 15);
            k1l *= C2;

            h1 ^= k1l;
            h1 =  Rl(h1, 13);
            h1 =  h1 * 5 + 0xe6546b64;
        }

        if (tailLen > 0) {
            uint num = 0;

            switch (tailLen) {
                case 1:
                    num ^= bytes[position];
                    break;
                case 2:
                    num ^= (uint)bytes[position + 1] << 8;
                    goto case 1;
                case 3:
                    num ^= (uint)bytes[position + 2] << 16;
                    goto case 2;
            }

            h1 ^= Rl(num * C1, 15) * C2;
        }

        h1 = FMix(h1 ^ (uint)length);

        return h1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Rl(uint x, byte r) => x << r | x >> 32 - r;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint FMix(uint h) {
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return h;
    }
}