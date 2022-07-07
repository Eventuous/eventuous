// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

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

class HandlersMap<TAggregate> : Dictionary<Type, RegisteredHandler<TAggregate>>
    where TAggregate : Aggregate {
    public void AddHandler<TCommand>(RegisteredHandler<TAggregate> handler) {
        if (ContainsKey(typeof(TCommand))) {
            EventuousEventSource.Log.CommandHandlerAlreadyRegistered<TCommand>();
            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }

        Add(typeof(TCommand), handler);
    }

    public void AddHandler<TCommand>(ExpectedState expectedState, ActOnAggregateAsync<TAggregate, TCommand> action) {
        AddHandler<TCommand>(
            new RegisteredHandler<TAggregate>(
                expectedState,
                async (aggregate, cmd, ct) => {
                    await action(aggregate, (TCommand)cmd, ct).NoContext();
                    return aggregate;
                }
            )
        );
        //
        // static async ValueTask<TAggregate> AsTask(
        //     TAggregate                                aggregate,
        //     TCommand                                  cmd,
        //     ActOnAggregateAsync<TAggregate, TCommand> action,
        //     CancellationToken                         cancellationToken
        // ) {
        //     await action(aggregate, cmd, cancellationToken).NoContext();
        //     return aggregate;
        // }
    }

    public void AddHandler<TCommand>(ExpectedState expectedState, ActOnAggregate<TAggregate, TCommand> action)
        => AddHandler<TCommand>(
            new RegisteredHandler<TAggregate>(
                expectedState,
                (aggregate, cmd, _) => {
                    action(aggregate, (TCommand)cmd);
                    return new ValueTask<TAggregate>(aggregate);
                }
            )
        );

    // static ValueTask<TAggregate> SyncAsTask<TCommand>(
    //     TAggregate                           aggregate,
    //     TCommand                             cmd,
    //     ActOnAggregate<TAggregate, TCommand> action
    // ) {
    //     action(aggregate, cmd);
    //     return new ValueTask<TAggregate>(aggregate);
    // }
}
