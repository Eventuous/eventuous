using System.Runtime.CompilerServices;

namespace Eventuous;

public static class TaskExtensions {
    public static ConfiguredTaskAwaitable NoContext(this Task task) => task.ConfigureAwait(false);

    public static ConfiguredTaskAwaitable<T> NoContext<T>(this Task<T> task) => task.ConfigureAwait(false);

    public static ConfiguredValueTaskAwaitable NoContext(this ValueTask task) => task.ConfigureAwait(false);

    public static ConfiguredValueTaskAwaitable<T> NoContext<T>(this ValueTask<T> task) => task.ConfigureAwait(false);

    public static ConfiguredCancelableAsyncEnumerable<T> IgnoreWithCancellation<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken        cancellationToken
    )
        => source.WithCancellation(cancellationToken).ConfigureAwait(false);

    public static Task WhenAll(this IEnumerable<Task> tasks) => Task.WhenAll(tasks);

    public static async Task WhenAll(this IEnumerable<ValueTask> tasks) {
        var toAwait = tasks
            .Where(valueTask => !valueTask.IsCompletedSuccessfully)
            .Select(valueTask => valueTask.AsTask())
            .ToList();

        if (toAwait.Count > 0) await Task.WhenAll(toAwait).NoContext();
    }

    public static async Task<IReadOnlyCollection<T>> WhenAll<T>(this IEnumerable<ValueTask<T>> tasks) {
        var results = new List<T>();
        var toAwait = new List<Task<T>>();

        foreach (var valueTask in tasks) {
            if (valueTask.IsCompletedSuccessfully) results.Add(valueTask.Result);
            else toAwait.Add(valueTask.AsTask());
        }

        if (toAwait.Count == 0) return results;

        results.AddRange(await Task.WhenAll(toAwait).NoContext());

        return results;
    }
}
