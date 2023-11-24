// Copyright (C) Ubiquitous AS. All rights reserved
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
    public static async Task WhenAll(this IEnumerable<ValueTask> tasks) {
        var toAwait = tasks.Where(valueTask => !valueTask.IsCompletedSuccessfully).Select(valueTask => valueTask.AsTask());

        if (toAwait.Any()) await Task.WhenAll(toAwait).NoContext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<IReadOnlyCollection<T>> WhenAll<T>(this IEnumerable<ValueTask<T>> tasks) {
        var results = new List<T>();
        var toAwait = new List<Task<T>>();

        foreach (var valueTask in tasks) {
            if (valueTask.IsCompletedSuccessfully)
                results.Add(valueTask.Result);
            else
                toAwait.Add(valueTask.AsTask());
        }

        if (toAwait.Count == 0) return results;

        results.AddRange(await Task.WhenAll(toAwait).NoContext());

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
