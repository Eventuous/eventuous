namespace Eventuous; 

/// <summary>
/// State store allows loading an aggregate state from any stream. The idea is to be able to load a different
/// version of the state than used in the aggregate itself, like an in-memory projection.
/// </summary>
[PublicAPI]
public interface IStateStore {
    /// <summary>
    /// Load the aggregate state from the store, without initialising the aggregate itself
    /// </summary>
    /// <param name="stream">Aggregate event</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<T> LoadState<T>(StreamName stream, CancellationToken cancellationToken) where T : State<T>, new();
}