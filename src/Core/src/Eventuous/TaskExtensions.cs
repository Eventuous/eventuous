using System.Runtime.CompilerServices;

namespace Eventuous; 

public static class TaskExtensions {
    public static ConfiguredTaskAwaitable NoContext(this Task task) => task.ConfigureAwait(false);
        
    public static ConfiguredTaskAwaitable<T> NoContext<T>(this Task<T> task) => task.ConfigureAwait(false);
        
    public static ConfiguredValueTaskAwaitable NoContext(this ValueTask task) => task.ConfigureAwait(false);
        
    public static ConfiguredValueTaskAwaitable<T> NoContext<T>(this ValueTask<T> task) => task.ConfigureAwait(false);

    public static ConfiguredCancelableAsyncEnumerable<T> IgnoreWithCancellation<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken)
        => source.WithCancellation(cancellationToken).ConfigureAwait(false);
}