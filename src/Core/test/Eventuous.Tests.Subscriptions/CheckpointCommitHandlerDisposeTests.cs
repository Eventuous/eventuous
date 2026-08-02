using Eventuous.Subscriptions.Checkpoints;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Guards against AI-1699, where host shutdown failed with
/// <c>ObjectDisposedException: The CancellationTokenSource has been disposed</c> thrown from
/// <c>ChannelWorkerBase.DisposeAsync</c> via <see cref="CheckpointCommitHandler.DisposeAsync"/>.
/// The commit handler can be disposed twice — <c>Resubscribe</c> and <c>Finalize</c> both call
/// <c>DisposeCommitHandler</c>, and the second call must be a no-op rather than a crash.
/// </summary>
public class CheckpointCommitHandlerDisposeTests {
    [Test]
    public async Task Dispose_is_idempotent() {
        var store   = new NoOpCheckpointStore();
        var handler = new CheckpointCommitHandler("test-dispose-twice", store, TimeSpan.FromMilliseconds(10));

        await handler.DisposeAsync();

        await Should.NotThrowAsync(async () => await handler.DisposeAsync());
    }

    /// <summary>
    /// The loser of the dispose race must not report the worker stopped before it actually is —
    /// host shutdown continues on that return, and the final checkpoint flush is still in flight.
    /// </summary>
    [Test]
    public async Task Second_dispose_awaits_the_first(CancellationToken ct) {
        var storeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release      = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new CheckpointCommitHandler(
            "test-dispose-awaits",
            async (checkpoint, _, _) => {
                storeEntered.TrySetResult();
                await release.Task;

                return checkpoint;
            },
            TimeSpan.FromMilliseconds(1)
        );

        // Park the commit worker inside the checkpoint store so the first dispose can't complete.
        await handler.Commit(new CommitPosition(0, 0, DateTime.UtcNow), ct);
        await storeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);

        var first  = handler.DisposeAsync();
        var second = handler.DisposeAsync();

        await Task.Delay(200, ct);
        second.IsCompleted.ShouldBeFalse("the second dispose must await the shutdown in flight, not skip past it");

        release.SetResult();
        await first;
        await second;
    }
}
