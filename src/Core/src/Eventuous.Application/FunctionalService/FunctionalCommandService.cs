// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.FuncServiceDelegates;

namespace Eventuous;

using static Diagnostics.ApplicationEventSource;

public abstract class FunctionalCommandService<TState>(IEventReader reader, IEventWriter writer, TypeMapper? typeMap = null)
    : IFuncCommandService<TState>, IStateCommandService<TState> where TState : State<TState>, new() {
    [PublicAPI]
    protected IEventReader Reader { get; } = reader;
    [PublicAPI]
    protected IEventWriter Writer { get; } = writer;

    readonly TypeMapper              _typeMap  = typeMap ?? TypeMap.Instance;
    readonly FuncHandlersMap<TState> _handlers = new();

    bool _initialized;

    protected FunctionalCommandService(IEventStore store, TypeMapper? typeMap = null)
        : this(store, store, typeMap) { }

    /// <summary>
    /// Returns the command handler builder for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    protected FuncCommandHandlerBuilder<TCommand, TState> On<TCommand>() where TCommand : class {
        var builder = new FuncCommandHandlerBuilder<TCommand, TState>(Reader, Writer);
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

    public async Task<Result<TState>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class {
        if (!_initialized) BuildHandlers();

        if (!_handlers.TryGet<TCommand>(out var registeredHandler)) {
            Log.CommandHandlerNotFound<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound<TCommand>();

            return new ErrorResult<TState>(exception);
        }

        var streamName = await registeredHandler.GetStream(command, cancellationToken).NoContext();

        try {
            var loadedState = registeredHandler.ExpectedState switch {
                ExpectedState.Any      => await Reader.LoadStateOrNew<TState>(streamName, cancellationToken).NoContext(),
                ExpectedState.Existing => await Reader.LoadState<TState>(streamName, cancellationToken).NoContext(),
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

            var storeResult = await Writer.Store(streamName, (int)loadedState.StreamVersion.Value, newEvents, static e => e, cancellationToken).NoContext();
            var changes     = newEvents.Select(x => new Change(x, _typeMap.GetTypeName(x)));
            Log.CommandHandled<TCommand>();

            return new OkResult<TState>(newState, changes, storeResult.GlobalPosition);
        } catch (Exception e) {
            Log.ErrorHandlingCommand<TCommand>(e);

            return new ErrorResult<TState>($"Error handling command {typeof(TCommand).Name}", e);
        }
    }

    async Task<Result> ICommandService.Handle<TCommand>(TCommand command, CancellationToken cancellationToken) {
        var result = await Handle(command, cancellationToken).NoContext();

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
