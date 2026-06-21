using System.Text.Json;

namespace Eventuous.Azure.Storage.Blobs;

public class StorageBlobProjectorOptions<T> where T : class, new() {
    public JsonSerializerOptions? JsonOptions { get; set; }
    public Func<BinaryData, T>? Deserialize { get; set; }
    public Func<T, byte[]>? Serialize { get; set; }
}
