// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static partial class FuncServiceDelegates {
    internal delegate ValueTask<StreamName> GetStreamNameFromUntypedCommand(object command, CancellationToken cancellationToken);

    internal delegate ValueTask<IEnumerable<object>> ExecuteUntypedCommand<in T>(T state, object[] events, object command, CancellationToken cancellationToken)
        where T : State<T>;

    internal delegate IEventReader ResolveReaderFromCommand(object command);

    internal delegate IEventWriter ResolveWriterFromCommand(object command);

    internal delegate StreamEvent AmendEventFromCommand(StreamEvent streamEvent, object command);
}
