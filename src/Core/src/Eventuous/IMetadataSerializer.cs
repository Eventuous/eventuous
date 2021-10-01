namespace Eventuous; 

public interface IMetadataSerializer {
    byte[] Serialize(Metadata evt);

    Metadata? Deserialize(ReadOnlySpan<byte> bytes);
}