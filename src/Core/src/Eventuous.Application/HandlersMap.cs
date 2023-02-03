// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using Eventuous.Tools;
using static Eventuous.Diagnostics.ApplicationEventSource;

namespace Eventuous;

public delegate Task ActOnAggregateAsync<in TAggregate, in TCommand>(
    TAggregate        aggregate,
    TCommand          command,
    CancellationToken cancellationToken
) where TAggregate : Aggregate;

public delegate void ActOnAggregate<in TAggregate, in TCommand>(TAggregate aggregate, TCommand command)
    where TAggregate : Aggregate;

record RegisteredHandler<T>(ExpectedState ExpectedState, Func<T, object, CancellationToken, ValueTask<T>> Handler);

class HandlersMap<TAggregate> where TAggregate : Aggregate {
    readonly TypeMap<RegisteredHandler<TAggregate>>          _typeMap  = new();
    readonly Dictionary<Type, RegisteredHandler<TAggregate>> _handlers = new();

    public void AddHandler<TCommand>(RegisteredHandler<TAggregate> handler) {
        try {
            _typeMap.Add<TCommand>(handler);
            _handlers.Add(typeof(TCommand), handler);
            Log.CommandHandlerRegistered<TCommand>();
        }
        catch (Exceptions.DuplicateTypeException<TCommand>) {
            Log.CommandHandlerAlreadyRegistered<TCommand>();
            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }
    }

    public void AddHandler<TCommand>(ExpectedState expectedState, ActOnAggregateAsync<TAggregate, TCommand> action)
        => AddHandler<TCommand>(
            new RegisteredHandler<TAggregate>(
                expectedState,
                async (aggregate, cmd, ct) => {
                    await action(aggregate, (TCommand)cmd, ct).NoContext();
                    return aggregate;
                }
            )
        );

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

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredHandler<TAggregate>? handler)
        => _typeMap.TryGetValue<TCommand>(out handler);
}
