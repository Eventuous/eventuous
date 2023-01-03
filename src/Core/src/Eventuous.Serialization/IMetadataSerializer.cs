namespace Eventuous; 

public interface IMetadataSerializer {
    byte[] Serialize(Metadata evt);

    /// <summary>
    /// Deserializes the metadata
    /// </summary>
    /// <param name="bytes">Serialized metadata as bytes</param>
    /// <returns>Deserialized metadata object</returns>
    /// <throws>MetadataDeserializationException if the metadata cannot be deserialized</throws>
    Metadata? Deserialize(ReadOnlySpan<byte> bytes);
}