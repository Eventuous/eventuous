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
}
