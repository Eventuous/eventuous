namespace Eventuous;

public static class AsyncHelper {
    static readonly TaskFactory MyTaskFactory = new(
        CancellationToken.None,
        TaskCreationOptions.None,
        TaskContinuationOptions.None,
        TaskScheduler.Default
    );

    public static TResult RunSync<TResult>(Func<Task<TResult>> func)
        => MyTaskFactory
            .StartNew(func)
            .Unwrap()
            .GetAwaiter()
            .GetResult();

    public static void RunSync(Func<Task> func)
        => MyTaskFactory
            .StartNew(func)
            .Unwrap()
            .GetAwaiter()
            .GetResult();
}
