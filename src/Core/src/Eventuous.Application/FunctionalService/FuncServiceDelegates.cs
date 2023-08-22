// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static partial class FuncServiceDelegates {
    public delegate StreamName GetStreamNameFromCommand<in TCommand>(TCommand command) where TCommand : class;

    public delegate Task<StreamName> GetStreamNameFromCommandAsync<in TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class;

    internal delegate ValueTask<StreamName> GetStreamNameFromUntypedCommand(object command, CancellationToken cancellationToken);

    internal delegate ValueTask<IEnumerable<object>> ExecuteUntypedCommand<in T>(T state, object[] events, object command, CancellationToken cancellationToken)
        where T : State<T>;

    public delegate IEnumerable<object> ExecuteCommand<in T, in TCommand>(T state, object[] originalEvents, TCommand command)
        where T : State<T> where TCommand : class;

    public delegate Task<IEnumerable<object>> ExecuteCommandAsync<in T, in TCommand>(
            T                 state,
            object[]          originalEvents,
            TCommand          command,
            CancellationToken cancellationToken
        )
        where T : State<T> where TCommand : class;

    public delegate IEventReader ResolveReaderFromCommand<in TCommand>(TCommand command) where TCommand : class;

    internal delegate IEventReader ResolveReaderFromCommand(object command);

    public delegate IEventWriter ResolveWriterFromCommand<in TCommand>(TCommand command) where TCommand : class;

    public delegate IEventStore ResolveEventStoreFromCommand<in TCommand>(TCommand command) where TCommand : class;

    internal delegate IEventWriter ResolveWriterFromCommand(object command);
}
