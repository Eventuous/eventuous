using Eventuous.Diagnostics;

namespace Eventuous;

public delegate Task ActOnAggregateAsync<in TAggregate, in TCommand>(
    TAggregate        aggregate,
    TCommand          command,
    CancellationToken cancellationToken
) where TAggregate : Aggregate;

public delegate void ActOnAggregate<in TAggregate, in TCommand>(TAggregate aggregate, TCommand command)
    where TAggregate : Aggregate;

record RegisteredHandler<T>(ExpectedState ExpectedState, Func<T, object, CancellationToken, ValueTask<T>> Handler);

class HandlersMap<TAggregate> : Dictionary<Type, RegisteredHandler<TAggregate>> where TAggregate : Aggregate {
    public void AddHandler<TCommand>(RegisteredHandler<TAggregate> handler) {
        if (ContainsKey(typeof(TCommand))) {
            EventuousEventSource.Log.CommandHandlerAlreadyRegistered<TCommand>();
            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }

        Add(typeof(TCommand), handler);
    }

    public void AddHandler<TCommand>(ExpectedState expectedState, ActOnAggregateAsync<TAggregate, TCommand> action)
        => AddHandler<TCommand>(
            new RegisteredHandler<TAggregate>(expectedState, (aggregate, cmd, ct) => AsTask(aggregate, cmd, action, ct))
        );

    public void AddHandler<TCommand>(ExpectedState expectedState, ActOnAggregate<TAggregate, TCommand> action)
        => AddHandler<TCommand>(
            new RegisteredHandler<TAggregate>(expectedState, (aggregate, cmd, _) => SyncAsTask(aggregate, cmd, action))
        );

    static async ValueTask<TAggregate> AsTask<TCommand>(
        TAggregate                                aggregate,
        object                                    cmd,
        ActOnAggregateAsync<TAggregate, TCommand> action,
        CancellationToken                         cancellationToken
    ) {
        await action(aggregate, (TCommand)cmd, cancellationToken).NoContext();
        return aggregate;
    }

    static ValueTask<TAggregate> SyncAsTask<TCommand>(
        TAggregate                           aggregate,
        object                               cmd,
        ActOnAggregate<TAggregate, TCommand> action
    ) {
        action(aggregate, (TCommand)cmd);
        return new ValueTask<TAggregate>(aggregate);
    }
}
