// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

// ReSharper disable PossibleMultipleEnumeration

namespace Eventuous.Tools;

static class TaskExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable NoContext(this Task task) => task.ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable<T> NoContext<T>(this Task<T> task) => task.ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredValueTaskAwaitable NoContext(this ValueTask task) => task.ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredValueTaskAwaitable<T> NoContext<T>(this ValueTask<T> task) => task.ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredCancelableAsyncEnumerable<T> NoContext<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken)
        => source.WithCancellation(cancellationToken).ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task WhenAll(this IEnumerable<Task> tasks) => Task.WhenAll(tasks);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask WhenAll(this IEnumerable<ValueTask> tasks) {
        return tasks is ValueTask[] array ? AwaitArray(array) : AwaitArray(tasks.ToArray());

        // ReSharper disable once SuggestBaseTypeForParameter
        async ValueTask AwaitArray(ValueTask[] t) {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < t.Length; i++) {
                await t[i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<IReadOnlyCollection<T>> WhenAll<T>(this IEnumerable<ValueTask<T>> tasks) {
        var results = new List<T>();

        foreach (var valueTask in tasks) {
            results.Add(valueTask.IsCompletedSuccessfully ? valueTask.Result : await valueTask);
        }

        return results;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable NoThrow(this Task task) {
#if NET8_0_OR_GREATER
        return task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
#else
        return Try(task.ConfigureAwait(false)).ConfigureAwait(false);

        async Task Try(ConfiguredTaskAwaitable awaitable) {
            try { await awaitable; } catch (OperationCanceledException) { }
        }
#endif
    }
}
