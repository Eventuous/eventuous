namespace Eventuous;

public delegate StreamName GetStreamFromCommand<in TCommand>(TCommand command);

public delegate Task<TId> GetIdFromCommandAsync<TId, in TCommand>(
    TCommand          command,
    CancellationToken cancellationToken
) where TId : AggregateId;

public delegate TId GetIdFromCommand<out TId, in TCommand>(TCommand command) where TId : AggregateId;

class StreamNameMap : Dictionary<Type, Func<object, CancellationToken, ValueTask<StreamName>>> {
    public void AddCommand<TCommand>(Func<TCommand, CancellationToken, ValueTask<StreamName>> getStreamName)
        where TCommand : class
        => TryAdd(typeof(TCommand), (obj, token) => getStreamName((TCommand)obj, token));

    public void AddCommand<TCommand>(GetStreamFromCommand<TCommand> getStreamFromCommand) where TCommand : class
        => TryAdd(typeof(TCommand), (obj, _) => new ValueTask<StreamName>(getStreamFromCommand((TCommand)obj)));

    public void AddCommand<TId, TCommand>(GetIdFromCommand<TId, TCommand> getId) where TId : AggregateId
        => TryAdd(
            typeof(TCommand),
            (cmd, _) => new ValueTask<StreamName>(new StreamName(getId((TCommand)cmd)))
        );

    public void AddCommand<TId, TCommand>(GetIdFromCommandAsync<TId, TCommand> getId) where TId : AggregateId
        => TryAdd(
            typeof(TCommand),
            async (cmd, ct) => new StreamName(await getId((TCommand)cmd, ct))
        );
}
