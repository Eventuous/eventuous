// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

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