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
    Task<AppendEventsResult> Store<T>(T aggregate, CancellationToken cancellationToken) where T : Aggregate;

    /// <summary>
    /// Load the aggregate from the store for a given id
    /// </summary>
    /// <param name="id">Aggregate id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns></returns>
    Task<T> Load<T>(string id, CancellationToken cancellationToken) where T : Aggregate, new();
}