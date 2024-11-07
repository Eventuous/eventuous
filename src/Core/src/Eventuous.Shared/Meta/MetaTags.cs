// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class MetaTags {
    const string Prefix = "eventuous";

    public const string MessageId     = $"{Prefix}.message-id";
    public const string CorrelationId = $"{Prefix}.correlation-id";
    public const string CausationId   = $"{Prefix}.causation-id";
}
