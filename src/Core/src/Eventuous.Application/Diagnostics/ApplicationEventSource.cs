// Copyright (C) Ubiquitous AS. All rights reserved
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
    const int CannotGetAggregateIdFromCommandId = 11;
    
    [NonEvent]
    public void CommandHandlerNotFound(Type type) => CommandHandlerNotFound(type.Name);

    [NonEvent]
    public void CannotCalculateAggregateId(Type type) => CannotCalculateAggregateId(type.Name);

    [NonEvent]
    public void ErrorHandlingCommand(Type type, Exception e) => ErrorHandlingCommand(type.Name, e.ToString());

    [NonEvent]
    public void CommandHandled(Type commandType) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All)) CommandHandled(commandType.Name);
    }

    [NonEvent]
    public void CommandHandlerAlreadyRegistered<T>() => CommandHandlerAlreadyRegistered(typeof(T).Name);

    [Event(CommandHandlerNotFoundId, Message = "Handler not found for command: '{0}'", Level = EventLevel.Error)]
    public void CommandHandlerNotFound(string commandType) => WriteEvent(CommandHandlerNotFoundId, commandType);

    [Event(
        CannotGetAggregateIdFromCommandId,
        Message = "Cannot get aggregate id from command: '{0}'",
        Level = EventLevel.Error
    )]
    public void CannotCalculateAggregateId(string commandType)
        => WriteEvent(CannotGetAggregateIdFromCommandId, commandType);

    [Event(ErrorHandlingCommandId, Message = "Error handling command: '{0}' {1}", Level = EventLevel.Error)]
    public void ErrorHandlingCommand(string commandType, string exception)
        => WriteEvent(ErrorHandlingCommandId, commandType, exception);

    [Event(CommandHandledId, Message = "Command handled: '{0}'", Level = EventLevel.Verbose)]
    public void CommandHandled(string commandType) => WriteEvent(CommandHandledId, commandType);

    [Event(
        CommandHandlerAlreadyRegisteredId,
        Message = "Command handler already registered for {0}",
        Level = EventLevel.Critical
    )]
    public void CommandHandlerAlreadyRegistered(string type) => WriteEvent(CommandHandlerAlreadyRegisteredId, type);

}