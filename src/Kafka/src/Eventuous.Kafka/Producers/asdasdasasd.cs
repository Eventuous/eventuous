namespace Eventuous.Kafka.Schema;

using System;
using Confluent.Kafka;
using static System.Array;

public delegate (string MessageType, string Encoding) GetSerializationInfo(Dictionary<string, string> headers);

public delegate byte[] SerializeMessage(object? data, headers);


public sealed class MessageSerializer : ISerializer<object?> {
    public MessageSerializer(GetSerializationInfo getSerializationInfo)
    {
        GetSerializationInfo = getSerializationInfo;
    }

    GetSerializationInfo GetSerializationInfo { get; }


    public byte[] Serialize(object? data, SerializationContext context)
    {
        switch (data)
        {
            case null: return Empty<byte>();
            case byte[] bytes: return bytes;
            case Memory<byte> bytes: return bytes.ToArray();
        }

        var headers = context.Headers.Decode();
        
        var info = GetSerializationInfo(headers);

        return SchemaRegistry.Global.Serialize(data, info.Encoding);
    }
}