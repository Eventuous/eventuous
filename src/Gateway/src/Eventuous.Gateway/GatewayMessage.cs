// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Gateway;

public record GatewayMessage<TProduceOptions>(StreamName TargetStream, object Message, Metadata? Metadata, TProduceOptions ProduceOptions);
