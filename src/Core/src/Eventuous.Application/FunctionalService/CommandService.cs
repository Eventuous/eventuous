// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

[Obsolete("Use CommandService<TState>")]
public abstract class FunctionalCommandService<TState>(IEventReader reader, IEventWriter writer, ITypeMapper? typeMap = null, AmendEvent? amendEvent = null)
    : CommandService<TState>(reader, writer, typeMap, amendEvent) where TState : State<TState>, new() {
    protected FunctionalCommandService(IEventStore store, ITypeMapper? typeMap = null, AmendEvent? amendEvent = null)
        : this(store, store, typeMap, amendEvent) { }

    [Obsolete("Use On<TCommand>().InState(ExpectedState.New).GetStream(...).Act(...) instead")]
    protected void OnNew<TCommand>(Func<TCommand, StreamName> getStreamName, Func<TCommand, NewEvents> action) where TCommand : class
        => On<TCommand>().InState(ExpectedState.New).GetStream(getStreamName).Act(action);

    [Obsolete("Use On<TCommand>().InState(ExpectedState.Existing).GetStream(...).Act(...) instead")]
    protected void OnExisting<TCommand>(Func<TCommand, StreamName> getStreamName, Func<TState, object[], TCommand, NewEvents> action)
        where TCommand : class
        => On<TCommand>().InState(ExpectedState.Existing).GetStream(getStreamName).Act(action);

    [Obsolete("Use On<TCommand>().InState(ExpectedState.Any).GetStream(...).Act(...) instead")]
    protected void OnAny<TCommand>(Func<TCommand, StreamName> getStreamName, Func<TState, object[], TCommand, NewEvents> action)
        where TCommand : class
        => On<TCommand>().InState(ExpectedState.Any).GetStream(getStreamName).Act(action);
}

/// <summary>
/// Base class for a functional command service for a given <seealso cref="State{T}"/> type.
/// Add your command handlers to the service using <see cref="On{TCommand}"/>.
/// </summary>
/// <param name="reader">Event reader or event store</param>
/// <param name="writer">Event writer or event store</param>
/// <param name="typeMap"><seealso cref="ITypeMapper"/> instance or null to use the default type mapper</param>
/// <param name="amendEvent">Optional function to add extra information to the event before it gets stored</param>
/// <typeparam name="TState">State object type</typeparam>
public abstract class CommandService<TState>(IEventReader reader, IEventWriter writer, ITypeMapper? typeMap = null, AmendEvent? amendEvent = null)
    : ICommandService<TState> where TState : State<TState>, new() {
    readonly ITypeMapper         _typeMap  = typeMap ?? TypeMap.Instance;
    readonly HandlersMap<TState> _handlers = new();

    /// <summary>
    /// Alternative constructor for the functional command service, which uses an <seealso cref="IEventStore"/> instance for both reading and writing.
    /// </summary>
    /// <param name="store">Event store</param>
    /// <param name="typeMap"><seealso cref="ITypeMapper"/> instance or null to use the default type mapper</param>
    /// <param name="amendEvent">Optional function to add extra information to the event before it gets stored</param>
    // ReSharper disable once UnusedMember.Global
    protected CommandService(IEventStore store, ITypeMapper? typeMap = null, AmendEvent? amendEvent = null) : this(store, store, typeMap, amendEvent) { }

    /// <summary>
    /// Returns the command handler builder for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    protected IDefineExpectedState<TCommand, TState> On<TCommand>() where TCommand : class => new CommandHandlerBuilder<TCommand, TState>(this, reader, writer);

    /// <summary>
    /// Function to handle a command and return the resulting state and changes.
    /// </summary>
    /// <param name="command">Command to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns><seealso cref="Result{TState}"/> instance</returns>
    /// <exception cref="ArgumentOutOfRangeException">Throws when there's no command handler was registered for the command type</exception>
    public async Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class {
        if (!_handlers.TryGet<TCommand>(out var registeredHandler)) {
            Log.CommandHandlerNotFound<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound<TCommand>();

            return Result<TState>.FromError(exception);
        }

        var streamName     = await registeredHandler.GetStream(command, cancellationToken).NoContext();
        var resolvedReader = registeredHandler.ResolveReaderFromCommand(command);
        var resolvedWriter = registeredHandler.ResolveWriterFromCommand(command);

        try {
            var loadedState = registeredHandler.ExpectedState switch {
                ExpectedState.Any      => await resolvedReader.LoadState<TState>(streamName, false, cancellationToken).NoContext(),
                ExpectedState.Existing => await resolvedReader.LoadState<TState>(streamName, true, cancellationToken).NoContext(),
                ExpectedState.New      => new(streamName, ExpectedStreamVersion.NoStream, []),
                _                      => throw new ArgumentOutOfRangeException(nameof(registeredHandler.ExpectedState), "Unknown expected state")
            };

            var result = await registeredHandler
                .Handler(loadedState.State, loadedState.Events, command, cancellationToken)
                .NoContext();

            var newEvents = result.ToArray();
            var newState  = newEvents.Aggregate(loadedState.State, (current, evt) => current.When(evt));

            // Zero in the global position would mean nothing, so the receiver needs to check the Changes.Length
            if (newEvents.Length == 0) return Result<TState>.FromSuccess(newState, Array.Empty<Change>(), 0);

            var storeResult = await resolvedWriter.Store(streamName, loadedState.StreamVersion, newEvents, Amend, cancellationToken)
                .NoContext();
            var changes = newEvents.Select(x => Change.FromEvent(x, _typeMap));
            Log.CommandHandled<TCommand>();

            return Result<TState>.FromSuccess(newState, changes, storeResult.GlobalPosition);
        } catch (Exception e) {
            Log.ErrorHandlingCommand<TCommand>(e);

            return Result<TState>.FromError(e, $"Error handling command {typeof(TCommand).Name}");
        }

        NewStreamEvent Amend(NewStreamEvent streamEvent) {
            var evt = registeredHandler.AmendEvent?.Invoke(streamEvent, command) ?? streamEvent;

            return amendEvent?.Invoke(evt) ?? evt;
        }
    }

    protected static StreamName GetStream(string id) => StreamName.ForState<TState>(id);

    internal void AddHandler<TCommand>(RegisteredHandler<TState> handler) where TCommand : class {
        _handlers.AddHandlerUntyped(typeof(TCommand), handler);
    }
}
