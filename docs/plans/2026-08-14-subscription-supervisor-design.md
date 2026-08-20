# Subscription Supervisor Design

## Goal

One universal shape for every subscription: a transport says how to connect — registering whatever it needs
torn down as it acquires it — and `EventSubscription<T>` owns a single sequential loop that decides when to
retry. A reader should be able to verify the lifecycle by reading `EventSubscription.Subscribe` and
`RunSubscriptionLoop` alone, without holding a state machine, a generation counter and a drop cycle in their
head at once.

`Subscribe(OnSubscribed, OnDropped, CancellationToken)` returns once the subscription is up, and everything
after that runs on one background loop per subscription — not one per drop. A transport's own failure signal
(a dropped connection, a dead poll loop) can fire from any thread, at any time, and there can be several
in flight if a connection flaps; collapsing them onto a single loop means there is exactly one place that
decides "retry" or "give up", instead of N concurrent retry attempts racing each other.

## The contract

```csharp
/// Connects the transport. Returns once up, throws if it can't come up. Called once per run and must be
/// repeatable on the same instance — reassign fields rather than assume them unset.
protected abstract ValueTask Connect(SubscriptionRun run);
```

`Connect` returning is readiness — `Subscribe` waits for the first `Connect` to return (or throw) before it
returns to its own caller. `Connect` throwing on the *first* attempt propagates out of `Subscribe`, unretried
— a caller who asks for a subscription that cannot come up should see the exception, not a silent retry loop.
A later `Connect` failure (on resubscribe) is an ordinary drop and is retried.

There is no `Disconnect` method to override. Teardown is registration-based: a transport calls
`run.OnDisconnect(...)` for each handle as it acquires it, and those callbacks run when the run ends —
in reverse registration order (see [Teardown](#teardown)). This means a `Connect` that fails part-way still
releases whatever it already took, and a transport never has to remember a matching "undo" method — it just
registers the undo next to the acquire.

A transport with its own loop (polling, a long-lived read) starts it on its own task and registers a join:

```csharp
// SqlSubscriptionBase.Connect
var pumping = Task.Run(async () => {
    try {
        await Poll(run, start, run.Token).NoContext();
    } catch (Exception) when (run.Token.IsCancellationRequested) {
        // This run's own token asked for it: graceful, not a drop.
    } catch (Exception e) {
        run.Fail(DropReason.ServerError, e);
    }
}, CancellationToken.None);

// No handle of its own to release: registered purely to join the loop before the next Connect starts.
run.OnDisconnect(_ => new(pumping));
```

A callback-driven transport just connects, wires the drop callback to `run.Fail`, and registers the handle it
was given:

```csharp
// StreamSubscription.Connect
var subscription = await Client.SubscribeToStreamAsync(
        Options.StreamName, fromStream, (_, @event, ct) => HandleEvent(@event, ct),
        Options.ResolveLinkTos, HandleDrop, Options.Credentials, run.Token)
    .NoContext();

run.OnDisconnect(_ => { subscription.Dispose(); return default; });

void HandleDrop(global::KurrentDB.Client.StreamSubscription _, SubscriptionDroppedReason reason, Exception? ex)
    => run.Fail(KurrentDBMappings.AsDropReason(reason), ex);
```

Both shapes end the same way: whatever ends the run — a reported drop, a dying loop, or the run's own token
being cancelled by shutdown — is observed once, by the supervisor, through `run.Ended`.

## The run

`SubscriptionRun` is the identity of one connect attempt. Every signal that can arrive late — a failure
report from a dead connection, an ack for a message dispatched two runs ago, a sequence number drawn by a
loop that is still winding down — is addressed to the run it was created under, not to "whichever run is
current". A run that has already ended is simply a run nobody is listening to any more; there is no separate
generation counter or gate to check, because the fencing is object identity.

```csharp
public class SubscriptionRun {
    public CancellationToken Token { get; }   // cancelled when this run ends; the token a transport gives its I/O
    public Task Ended => _ended.Task;         // completed when this run is over, for any reason

    public ulong NextSequence() => Interlocked.Increment(ref _sequence) - 1;

    public void Fail(DropReason reason, Exception? exception) {
        if (Interlocked.CompareExchange(ref _failure, new(reason, exception), null) is not null) return;
        _ended.TrySetResult();
    }

    public void OnDisconnect(Func<CancellationToken, ValueTask> release) { ... }
}
```

`Fail` is total: a compare-exchange and a `TrySetResult`, safe from any thread, safe on a run retired long
ago, safe inside a catch or a finally. It does not cancel, log, or invoke `OnDropped` — the supervisor reports
the drop, once, on its own stack, after observing `Ended`. That is what makes one flapping connection produce
one log line and one `OnDropped` call no matter how many messages or callbacks call `Fail` concurrently:
first writer wins, everyone else's call is a no-op.

`Ended` covers both a reported failure and shutdown through the same arm — the run's `CancellationTokenSource`
is registered to complete `_ended` too, so a cancelled lifetime resolves `Ended` exactly as `Fail` does. The
supervisor waits on one thing regardless of why the run is over.

`SubscriptionRun` is derivable (`protected internal` constructor) so a subscription can hang per-attempt state
off the run — `EventSubscriptionWithCheckpoint` does this for its commit handler (see
[Checkpoints](#checkpoints)) — instead of a field the next run would silently overwrite.

## Teardown

`SubscriptionRun.Stop(graceful, log)` is called once per run, from the supervisor's loop, whether the run is
being replaced or is the last one:

```csharp
internal async ValueTask Stop(CancellationToken graceful, LogContext log) {
    try { await _cts.CancelAsync().NoContext(); } catch (Exception e) { log.SubscriptionDisconnectFailed(e); }

    try { await Disconnect(graceful, log).NoContext(); }
    finally { _cts.Dispose(); }
}
```

The token is cancelled first, so production stops before anything else happens. Then every handle registered
through `OnDisconnect` releases — in **reverse registration order**: the last thing acquired is released
first, the first thing acquired is released last. This is why
`EventSubscriptionWithCheckpoint.CreateRun` registers the commit handler's disposal *before* `Connect` runs:
being registered first means it releases last, after every transport handle the subclass goes on to register.
Acknowledgements still landing while the transport is being torn down need the commit handler alive to
receive them; releasing it early would drop those commits on the floor.

**Teardown always runs to completion.** Every release is awaited; none is ever abandoned, skipped or left
running behind the supervisor's back. `Stop` returns only once the last one has finished.

`SubscriptionOptions.TeardownTimeout` — five seconds by default, one shared budget for the whole teardown —
is therefore **advisory, not a deadline**. The token it cancels means *"this teardown can no longer be
graceful: drop what is optional and finish quickly"*, not *"stop"*. A release that can honour that speeds up;
a release with essential work ignores it and runs to completion, and teardown waits.

```csharp
for (var i = releases.Count - 1; i >= 0; i--) {
    try { await releases[i](graceful).NoContext(); }
    catch (Exception e) { log.SubscriptionDisconnectFailed(e); }
}
```

The alternative — bounding the wait and carrying on — was tried and removed. Because the commit handler
releases last, it was always the first thing cut off, so an overrunning transport join left the final
checkpoint flush running detached while the supervisor moved on to the retry delay and the next run's
`GetCheckpoint`. Two writers for one checkpoint key, and no store guards against a stale one landing second,
so the checkpoint could move *backwards*. Waiting is what makes the flush-then-read ordering a fact rather
than a hope.

That ordering holds within one subscription lifecycle. Its one boundary is an `Unsubscribe` whose token
expires before teardown finishes: the session is discarded anyway — refusing every later `Subscribe` on
behalf of a hung teardown would be worse — so a re-subscribe after a timed-out stop can overlap the old
run's final flush. The stop is logged as timed out, which is the operator's cue that the window exists.

Each release is guarded individually, so one that throws is logged and the ones behind it still run.

## Checkpoints

`EventSubscriptionWithCheckpoint<T>.CreateRun` is `sealed`, so every run reaching that class carries its own
`CheckpointCommitHandler`:

```csharp
sealed class CheckpointedRun(CancellationToken lifetime, CheckpointCommitHandler checkpoint) : SubscriptionRun(lifetime) {
    internal CheckpointCommitHandler Checkpoint { get; } = checkpoint;
}

protected sealed override SubscriptionRun CreateRun(CancellationToken lifetime) {
    var run = new CheckpointedRun(lifetime, new(Options.SubscriptionId, CheckpointStore, ...));

    // Registered first, before Connect, so it's the first OnDisconnect registration — and release order
    // reverses registration order, so it releases LAST, after every transport handle.
    run.OnDisconnect(_ => run.Checkpoint.DisposeAsync());

    return run;
}
```

Because the cast in `Ack`/`Nack` is guaranteed by construction (every run handed to this class is a
`CheckpointedRun`), there is no null check or lookup at the commit site — the run passed into
`HandleInternal` is the one whose handler will receive the ack.

`CheckpointCommitHandler.Commit` returns `ValueTask<bool>`, not `ValueTask`:

```csharp
public ValueTask<bool> Commit(CommitPosition position, CancellationToken cancellationToken)
```

`false` means the handler had already stopped (its batching worker was closed) when the commit was attempted
— the position was never accepted into the pipeline. The caller must treat that as "not committed": in
`Ack`, a `false` result logs `MessageFromPreviousRunIgnored` and returns without acknowledging the message
further. This is the mechanism that keeps a late ack — one that completes after its run has already ended and
its handler disposed — from silently advancing a checkpoint it has no business advancing.

## Options

```csharp
public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);       // SubscriptionOptions
public TimeSpan TeardownTimeout { get; set; } = TimeSpan.FromSeconds(5);  // SubscriptionOptions
```

`RetryDelay` is how long the supervisor waits between a drop and the next `Connect`. `TeardownTimeout` is the
single budget described above, spent once per run in `Stop`. Both are on `SubscriptionOptions` rather than
overridable members on the transport base class — they are operating numbers an operator should be able to
reach through configuration, not implementation details a transport author overrides in code. Both fall back
to their defaults, with a logged warning, if set to an unusable negative value.
