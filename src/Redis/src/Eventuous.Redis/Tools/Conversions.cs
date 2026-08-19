// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Redis.Tools;

static class Conversions {
    public static long ToLong(this RedisValue value) {
        var (first, second) = new Split(Ensure.NotNull<string>(value).AsSpan());
        return long.Parse(first) * 10 + long.Parse(second);
    }

    // Redis stream ID components are unsigned 64-bit values, so both parts are parsed as ulong
    // and range-checked before conversion to the signed position
    public static long ToRevision(this RedisValue value) {
        var (first, second) = new Split(Ensure.NotNull<string>(value).AsSpan());
        var sequence = ulong.Parse(second);

        if (sequence > 9) {
            throw new NotSupportedException(
                $"Redis stream entry ID {value} can't be represented as a stream position: the position encoding only supports ID sequence numbers 0-9. " +
                "Entries with higher sequence numbers were written with auto-generated IDs by an older version of the store."
            );
        }

        var milliseconds = ulong.Parse(first);

        return milliseconds <= long.MaxValue / 10
            ? (long)milliseconds * 10 + (long)sequence
            : throw new NotSupportedException($"Redis stream entry ID {value} can't be represented as a stream position: the millisecond part is too large.");
    }

    public static ulong ToULong(this ReadOnlySpan<char> valueString) {
        var (first, second) = new Split(valueString);
        return ulong.Parse(first) * 10 + ulong.Parse(second);
    }

    public static RedisValue ToRedisValue(this long position)
        => position != 0 ? new RedisValue($"{position / 10}-{position % 10}") : "0-0";

    readonly ref struct Split {
        public ReadOnlySpan<char> First  { get; }
        public ReadOnlySpan<char> Second { get; }

        public Split(ReadOnlySpan<char> valueString) {
            var index = valueString.IndexOf('-');
            First  = valueString[..index];
            Second = valueString[(index + 1)..];
        }

        public void Deconstruct(out ReadOnlySpan<char> first, out ReadOnlySpan<char> second) {
            first  = First;
            second = Second;
        }
    }
}
