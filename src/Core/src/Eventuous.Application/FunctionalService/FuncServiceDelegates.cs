// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.
global using NewEvents = System.Collections.Generic.IEnumerable<object>;

namespace Eventuous;

public static partial class FuncServiceDelegates {
    internal delegate ValueTask<StreamName> GetStreamNameFromUntypedCommand(object command, CancellationToken cancellationToken);

    internal delegate ValueTask<NewEvents> ExecuteUntypedCommand<in T>(T state, object[] events, object command, CancellationToken cancellationToken)
        where T : State<T>;

    internal delegate IEventReader ResolveReaderFromCommand(object command);

    internal delegate IEventWriter ResolveWriterFromCommand(object command);

    internal delegate NewStreamEvent AmendEventFromCommand(NewStreamEvent streamEvent, object command);
}