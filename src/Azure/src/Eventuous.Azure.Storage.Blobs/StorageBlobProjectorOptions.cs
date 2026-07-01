using System.Text.Json;

namespace Eventuous.Azure.Storage.Blobs;

/// <summary>
/// Options for configuring the storage blob projector.
/// </summary>
/// <typeparam name="T">The projection state type, which must be a class with a parameterless constructor.</typeparam>
public class StorageBlobProjectorOptions<T> where T : class, new() {
    /// <summary>
    /// Gets or sets the JSON serializer options to use when serializing or deserializing projection state.
    /// By default, the default JSON serializer options will be used if this property is not set.
    /// </summary>
    public JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>
    /// Gets or sets a custom deserialization function for the projection state.
    /// If not set, the default JSON deserialization will be used with <see cref="JsonOptions"/>
    /// </summary>
    public Func<BinaryData, T>? Deserialize { get; set; }

    /// <summary>
    /// Gets or sets a custom serialization function for the projection state.
    /// If not set, the default JSON serialization will be used with <see cref="JsonOptions"/>
    /// </summary>
    public Func<T, byte[]>? Serialize { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts for race condition handling when saving projection state.
    /// Default is 0 (no retries).
    /// </summary>
    public int RaceRetries { get; set; } = 0;

    /// <summary>
    /// Gets or sets the idempotency mode for the projector. When enabled, the projector will skip processing
    /// if the blob already exists with a matching identifier (message ID or global position), preventing duplicate processing.
    /// Default is <see cref="IdempotencyMode.None"/> (no idempotency checking).
    /// </summary>
    public IdempotencyMode IdempotencyMode { get; set; } = IdempotencyMode.None;
}

/// <summary>
/// Controls how the projection handles idempotency to prevent duplicate message processing.
/// </summary>
public enum IdempotencyMode {
    /// <summary>
    /// No idempotency checks. The projector will always process messages and update blobs.
    /// Use when duplicate processing is acceptable or when external mechanisms ensure message uniqueness.
    /// </summary>
    None,

    /// <summary>
    /// Skips processing if the existing blob was created from a message at the same global position.
    /// Uses the <c>GlobalPosition</c> metadata stored with the blob for comparison.
    /// Effective for append-only event streams where global position uniquely identifies a message.
    /// </summary>
    ByGlobalPosition,

    /// <summary>
    /// Skips processing if the existing blob was created from the same message ID.
    /// Uses the <c>MessageId</c> metadata stored with the blob for comparison.
    /// More precise than position-based checks, works even if messages are processed out of order.
    /// Especially from external message queues where global position may not be available or reliable.
    /// </summary>
    ByMessageId
}