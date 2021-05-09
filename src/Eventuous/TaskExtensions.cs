using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Eventuous {
    public static class TaskExtensions {
        public static ConfiguredTaskAwaitable Ignore(this Task task) => task.ConfigureAwait(false);
        
        public static ConfiguredTaskAwaitable<T> Ignore<T>(this Task<T> task) => task.ConfigureAwait(false);
        
        public static ConfiguredValueTaskAwaitable Ignore(this ValueTask task) => task.ConfigureAwait(false);
        
        public static ConfiguredValueTaskAwaitable<T> Ignore<T>(this ValueTask<T> task) => task.ConfigureAwait(false);

        public static ConfiguredCancelableAsyncEnumerable<T> IgnoreWithCancellation<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken)
            => source.WithCancellation(cancellationToken).ConfigureAwait(false);
    }
}