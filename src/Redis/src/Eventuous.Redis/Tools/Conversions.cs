// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Redis.Tools;

static class Conversions {
    public static long ToLong(this RedisValue value) {
        var (first, second) = new Split(Ensure.NotNull<string>(value).AsSpan());
        return long.Parse(first) * 10 + long.Parse(second);
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
