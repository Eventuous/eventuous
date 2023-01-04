// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Gateway;

public record GatewayMessage(StreamName TargetStream, object Message, Metadata? Metadata);

[PublicAPI]
public record GatewayMessage<TProduceOptions>(
    StreamName      TargetStream,
    object          Message,
    Metadata?       Metadata,
    TProduceOptions ProduceOptions
) : GatewayMessage(TargetStream, Message, Metadata);