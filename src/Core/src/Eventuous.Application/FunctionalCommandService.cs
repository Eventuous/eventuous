// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using Tools;
using static Diagnostics.ApplicationEventSource;

public class FunctionalCommandService<T> : IFuncCommandService<T>, IStateCommandService<T> where T : State<T>, new() {
    [PublicAPI]
    protected IEventStore Store { get; }

    readonly TypeMapper               _typeMap;
    readonly FunctionalHandlersMap<T> _handlers  = new();
    readonly CommandToStreamMap       _streamMap = new();

    public FunctionalCommandService(IEventStore store, TypeMapper? typeMap = null) {
        Store    = store;
        _typeMap = typeMap ?? TypeMap.Instance;
    }

    protected void OnNew<TCommand>(
        GetStreamNameFromCommand<TCommand>  getStreamName,
        Func<TCommand, IEnumerable<object>> action
    ) where TCommand : class {
        _handlers.AddHandler<TCommand>(ExpectedState.New, (_, _, cmd) => action(cmd));
        _streamMap.AddCommand(getStreamName);
    }

    protected void OnExisting<TCommand>(
        GetStreamNameFromCommand<TCommand> getStreamName,
        ExecuteCommand<T, TCommand>        action
    ) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Existing, action);
        _streamMap.AddCommand(getStreamName);
    }

    protected void OnAny<TCommand>(
        GetStreamNameFromCommand<TCommand> getStreamName,
        ExecuteCommand<T, TCommand>        action
    ) where TCommand : class {
        _handlers.AddHandler(ExpectedState.Any, action);
        _streamMap.AddCommand(getStreamName);
    }

    public async Task<Result<T>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class {
        if (!_handlers.TryGet<TCommand>(out var registeredHandler)) {
            Log.CommandHandlerNotFound<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound<TCommand>();
            return new ErrorResult<T>(exception);
        }

        var hasGetStreamFunction = _streamMap.TryGet<TCommand>(out var getStreamName);

        if (!hasGetStreamFunction || getStreamName == null) {
            Log.CannotCalculateAggregateId<TCommand>();
            var exception = new Exceptions.CommandHandlerNotFound<TCommand>();
            return new ErrorResult<T>(exception);
        }

        var streamName = await getStreamName(command, cancellationToken).NoContext();

        try {
            var loadedState = registeredHandler.ExpectedState switch {
                ExpectedState.Any      => await Store.LoadStateOrNew<T>(streamName, cancellationToken).NoContext(),
                ExpectedState.Existing => await Store.LoadState<T>(streamName, cancellationToken).NoContext(),
                ExpectedState.New      => new FoldedEventStream<T>(streamName, ExpectedStreamVersion.NoStream, Array.Empty<object>()),
                _                      => throw new ArgumentOutOfRangeException(nameof(registeredHandler.ExpectedState), "Unknown expected state")
            };

            var result = await registeredHandler
                .Handler(loadedState.State, loadedState.Events, command, cancellationToken)
                .NoContext();

            var newEvents = result.ToArray();

            var newState = newEvents.Aggregate(loadedState.State, (current, evt) => current.When(evt));

            // Zero in the global position would mean nothing, so the receiver need to check the Changes.Length
            if (newEvents.Length == 0) return new OkResult<T>(newState, Array.Empty<Change>(), 0);

            var storeResult = await Store.Store(
                    streamName,
                    (int)loadedState.StreamVersion.Value,
                    newEvents,
                    static e => e,
                    cancellationToken
                )
                .NoContext();

            var changes = newEvents.Select(x => new Change(x, _typeMap.GetTypeName(x)));

            Log.CommandHandled<TCommand>();

            return new OkResult<T>(newState, changes, storeResult.GlobalPosition);
        }
        catch (Exception e) {
            Log.ErrorHandlingCommand<TCommand>(e);

            return new ErrorResult<T>($"Error handling command {typeof(TCommand).Name}", e);
        }
    }

    async Task<Result> ICommandService.Handle<TCommand>(TCommand command, CancellationToken cancellationToken) {
        var result = await Handle(command, cancellationToken).NoContext();

        return result switch {
            OkResult<T>(var state, var enumerable, _) => new OkResult(state, enumerable),
            ErrorResult<T> error                      => new ErrorResult(error.Message, error.Exception),
            _                                         => throw new ApplicationException("Unknown result type")
        };
    }
}
