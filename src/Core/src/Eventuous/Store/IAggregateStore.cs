namespace Eventuous;

/// <summary>
/// Aggregate state persistent store
/// </summary>
[PublicAPI]
public interface IAggregateStore {
    /// <summary>
    /// Store the new or updated aggregate state
    /// </summary>
    /// <param name="aggregate">Aggregate instance, which needs to be persisted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns></returns>
    public Task<AppendEventsResult> Store<T>(T aggregate, CancellationToken cancellationToken) where T : Aggregate
        => this.Store(StreamName.For<T>(aggregate.GetId()), aggregate, cancellationToken);

    /// <summary>
    /// Store the new or updated aggregate state
    /// </summary>
    /// <param name="streamName"></param>
    /// <param name="aggregate">Aggregate instance, which needs to be persisted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns></returns>
    Task<AppendEventsResult> Store<T>(StreamName streamName, T aggregate, CancellationToken cancellationToken)
        where T : Aggregate;

    /// <summary>
    /// Load the aggregate from the store for a given id
    /// </summary>
    /// <param name="id">Aggregate id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns></returns>
    public Task<T> Load<T>(string id, CancellationToken cancellationToken) where T : Aggregate
        => this.Load<T>(StreamName.For<T>(Ensure.NotEmptyString(id)), cancellationToken);

    /// <summary>
    /// Load the aggregate from the store for a given id
    /// </summary>
    /// <param name="streamName"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns></returns>
    Task<T> Load<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate;

    /// <summary>
    /// Attempts to load the aggregate from the store for a given id. If the aggregate is not found,
    /// a new instance of the aggregate is returned
    /// </summary>
    /// <param name="id">Aggregate id as string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns></returns>
    public Task<T> LoadOrNew<T>(string id, CancellationToken cancellationToken) where T : Aggregate
        => this.LoadOrNew<T>(StreamName.For<T>(Ensure.NotEmptyString(id)), cancellationToken);

    /// <summary>
    /// Attempts to load the aggregate from the store for a given id. If the aggregate is not found,
    /// a new instance of the aggregate is returned
    /// </summary>
    /// <param name="streamName">Name of the aggregate stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns></returns>
    Task<T> LoadOrNew<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate;
}
