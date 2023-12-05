// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.FuncServiceDelegates;

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

/// <summary>
/// Base class for a functional command service for a given <seealso cref="State{T}"/> type.
/// Add your command handlers to the service using <see cref="On{TCommand}"/>.
/// </summary>
/// <param name="reader">Event reader or event store</param>
/// <param name="writer">Event writer or event store</param>
/// <param name="typeMap"><seealso cref="TypeMapper"/> instance or null to use the default type mapper</param>
/// <param name="amendEvent">Optional function to add extra information to the event before it gets stored</param>
/// <typeparam name="TState">State object type</typeparam>
public abstract class FunctionalCommandService<TState>(IEventReader reader, IEventWriter writer, TypeMapper? typeMap = null, AmendEvent? amendEvent = null)
    : IFuncCommandService<TState>, IStateCommandService<TState> where TState : State<TState>, new() {
    readonly TypeMapper              _typeMap  = typeMap ?? TypeMap.Instance;
    readonly FuncHandlersMap<TState> _handlers = new();

    bool       _initialized;
    AmendEvent _amendEvent = amendEvent;

    /// <summary>
    /// Alternative constructor for the functional command service, which uses an <seealso cref="IEventStore"/> instance for both reading and writing.
    /// </summary>
    /// <param name="store">Event store</param>
    /// <param name="typeMap"><seealso cref="TypeMapper"/> instance or null to use the default type mapper</param>
    /// <param name="amendEvent">Optional function to add extra information to the event before it gets stored</param>
    protected FunctionalCommandService(IEventStore store, TypeMapper? typeMap = null, AmendEvent? amendEvent = null)
        : this(store, store, typeMap, amendEvent) { }

    /// <summary>
    /// Returns the command handler builder for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    protected FuncCommandHandlerBuilder<TCommand, TState> On<TCommand>() where TCommand : class {
        var builder = new FuncCommandHandlerBuilder<TCommand, TState>(reader, writer);
        _builders.Add(typeof(TCommand), builder);

        return builder;
    }

    [Obsolete("Use On<TCommand>().InState(ExpectedState.New).GetStream(...).Act(...) instead")]
    protected void OnNew<TCommand>(GetStreamNameFromCommand<TCommand> getStreamName, Func<TCommand, IEnumerable<object>> action) where TCommand : class
        => On<TCommand>().InState(ExpectedState.New).GetStream(getStreamName).Act(action);

    [Obsolete("Use On<TCommand>().InState(ExpectedState.Existing).GetStream(...).Act(...) instead")]
    protected void OnExisting<TCommand>(GetStreamNameFromCommand<TCommand> getStreamName, ExecuteCommand<TState, TCommand> action) where TCommand : class
        => On<TCommand>().InState(ExpectedState.Existing).GetStream(getStreamName).Act(action);

    [Obsolete("Use On<TCommand>().InState(ExpectedState.Any).GetStream(...).Act(...) instead")]
    protected void OnAny<TCommand>(GetStreamNameFromCommand<TCommand> getStreamName, ExecuteCommand<TState, TCommand> action) where TCommand : class
        => On<TCommand>().InState(ExpectedState.Any).GetStream(getStreamName).Act(action);

    /// <summary>
    /// Function to handle a command and return the resulting state and changes.
    /// </summary>
    /// <param name="command">Command to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns><seealso cref="Result{TState}"/> instance</returns>
    /// <exception cref="ArgumentOutOfRangeException">Throws when there's no command handler was registered for the command type</exception>
    public Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class =>
        Handle(command, Amend.Nothing, cancellationToken);
    
    /// <summary>
    /// Function to handle a command and return the resulting state and changes.
    /// </summary>
    /// <param name="command">Command to handle</param>
    /// <param name="amendEvent">Function to add additional information to the event before it's stored.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns><seealso cref="Result{TState}"/> instance</returns>
    /// <exception cref="ArgumentOutOfRangeException">Throws when there's no command handler was registered for the command type</exception>
    public async Task<Result<TState>> Handle<TCommand>(TCommand command, AmendEvent amendEvent, CancellationToken cancellationToken) where TCommand : class {
        if (!_initialized) BuildHandlers();

        if (!_handlers.TryGet<TCommand>(out var registeredHandler)) {
            Log.CommandHandlerNotFound<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound<TCommand>();

            return new ErrorResult<TState>(exception);
        }

        var streamName = await registeredHandler.GetStream(command, cancellationToken).NoContext();
        var reader     = registeredHandler.ResolveReaderFromCommand(command);
        var writer     = registeredHandler.ResolveWriterFromCommand(command);

        try {
            var loadedState = registeredHandler.ExpectedState switch {
                ExpectedState.Any      => await reader.LoadStateOrNew<TState>(streamName, cancellationToken).NoContext(),
                ExpectedState.Existing => await reader.LoadState<TState>(streamName, cancellationToken).NoContext(),
                ExpectedState.New      => new FoldedEventStream<TState>(streamName, ExpectedStreamVersion.NoStream, Array.Empty<object>()),
                _                      => throw new ArgumentOutOfRangeException(nameof(registeredHandler.ExpectedState), "Unknown expected state")
            };

            var result = await registeredHandler
                .Handler(loadedState.State, loadedState.Events, command, cancellationToken)
                .NoContext();

            var newEvents = result.ToArray();
            var newState  = newEvents.Aggregate(loadedState.State, (current, evt) => current.When(evt));

            // Zero in the global position would mean nothing, so the receiver need to check the Changes.Length
            if (newEvents.Length == 0) return new OkResult<TState>(newState, Array.Empty<Change>(), 0);

            var storeResult = await writer.Store(
                    streamName,
                    (int)loadedState.StreamVersion.Value,
                    newEvents,
                    @event => amendEvent(_amendEvent(@event)),
                    cancellationToken
                )
                .NoContext();
            var changes = newEvents.Select(x => new Change(x, _typeMap.GetTypeName(x)));
            Log.CommandHandled<TCommand>();

            return new OkResult<TState>(newState, changes, storeResult.GlobalPosition);
        } catch (Exception e) {
            Log.ErrorHandlingCommand<TCommand>(e);

            return new ErrorResult<TState>($"Error handling command {typeof(TCommand).Name}", e);
        }
    }

    async Task<Result> ICommandService.Handle<TCommand>(TCommand command, AmendEvent amendEvent, CancellationToken cancellationToken) {
        var result = await Handle(command, amendEvent, cancellationToken).NoContext();

        return result switch {
            OkResult<TState>(var state, var enumerable, _) => new OkResult(state, enumerable),
            ErrorResult<TState> error                      => new ErrorResult(error.Message, error.Exception),
            _                                              => throw new ApplicationException("Unknown result type")
        };
    }

    readonly Dictionary<Type, FuncCommandHandlerBuilder<TState>> _builders     = new();
    readonly object                                              _handlersLock = new();

    void BuildHandlers() {
        lock (_handlersLock) {
            foreach (var commandType in _builders.Keys) {
                var builder = _builders[commandType];
                var handler = builder.Build();
                _handlers.AddHandlerUntyped(commandType, handler);
            }

            _initialized = true;
        }
    }
}
