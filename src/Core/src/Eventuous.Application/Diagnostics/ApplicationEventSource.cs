// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Diagnostics;

[EventSource(Name = $"{DiagnosticName.BaseName}.application")]
class ApplicationEventSource : EventSource {
    public static ApplicationEventSource Log { get; } = new();

    const int CommandHandlerNotFoundId          = 1;
    const int ErrorHandlingCommandId            = 2;
    const int CommandHandledId                  = 3;
    const int CommandHandlerAlreadyRegisteredId = 4;
    const int CommandHandlerRegisteredId        = 5;

    [NonEvent]
    public void CommandHandlerNotFound<TCommand>() => CommandHandlerNotFound(typeof(TCommand).Name);

    [NonEvent]
    public void ErrorHandlingCommand<TCommand>(Exception e) => ErrorHandlingCommand(typeof(TCommand).Name, e.ToString());

    [NonEvent]
    public void CommandHandled<TCommand>() {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All)) CommandHandled(typeof(TCommand).Name);
    }

    [NonEvent]
    public void CommandHandlerAlreadyRegistered<T>() => CommandHandlerAlreadyRegistered(typeof(T).Name);

    [NonEvent]
    public void CommandHandlerRegistered<T>() {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All)) CommandHandlerRegistered(typeof(T).Name);
    }

    [Event(CommandHandlerNotFoundId, Message = "Handler not found for command: '{0}'", Level = EventLevel.Error)]
    void CommandHandlerNotFound(string commandType) => WriteEvent(CommandHandlerNotFoundId, commandType);

    [Event(ErrorHandlingCommandId, Message = "Error handling command: '{0}' {1}", Level = EventLevel.Error)]
    void ErrorHandlingCommand(string commandType, string exception) => WriteEvent(ErrorHandlingCommandId, commandType, exception);

    [Event(CommandHandledId, Message = "Command handled: '{0}'", Level = EventLevel.Verbose)]
    void CommandHandled(string commandType) => WriteEvent(CommandHandledId, commandType);

    [Event(CommandHandlerAlreadyRegisteredId, Message = "Command handler already registered for {0}", Level = EventLevel.Critical)]
    void CommandHandlerAlreadyRegistered(string type) => WriteEvent(CommandHandlerAlreadyRegisteredId, type);

    [Event(CommandHandlerRegisteredId, Message = "Command handler registered for {0}", Level = EventLevel.Verbose)]
    void CommandHandlerRegistered(string type) => WriteEvent(CommandHandlerRegisteredId, type);
}
