// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Persistence;
using static Eventuous.CommandServiceDelegates;
using static Eventuous.FuncServiceDelegates;

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

record RegisteredHandler<TAggregate, TState, TId>(
        ExpectedState                            ExpectedState,
        GetIdFromUntypedCommand<TId>             GetId,
        HandleUntypedCommand<TAggregate, TState> Handler,
        ResolveReaderFromCommand                 ResolveReader,
        ResolveWriterFromCommand                 ResolveWriter,
        AmendEventFromCommand?                   AmendEvent
    ) where TAggregate : Aggregate<TState> where TId : Id where TState : State<TState>, new() {
    public AmendAppend? AmendAppend { get; set; }
}

class HandlersMap<TAggregate, TState, TId> where TAggregate : Aggregate<TState> where TId : Id where TState : State<TState>, new() {
    readonly TypeMap<RegisteredHandler<TAggregate, TState, TId>> _typeMap = new();

    internal void AddHandler<TCommand>(RegisteredHandler<TAggregate, TState, TId> handler) {
        try {
            _typeMap.Add<TCommand>(handler);
            Log.CommandHandlerRegistered<TCommand>();
        } catch (Exceptions.DuplicateTypeException<TCommand>) {
            Log.CommandHandlerAlreadyRegistered<TCommand>();

            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }
    }

    public bool TryGet<TCommand>([NotNullWhen(true)] out RegisteredHandler<TAggregate, TState, TId>? handler) => _typeMap.TryGetValue<TCommand>(out handler);
}
