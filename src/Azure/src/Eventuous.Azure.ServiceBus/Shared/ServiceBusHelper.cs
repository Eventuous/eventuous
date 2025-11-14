// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Azure.ServiceBus.Shared;

public static class ServiceBusHelper {
    public static bool IsSerialisableByServiceBus(object? value) =>
        value is string
            or bool
            or byte
            or sbyte
            or short
            or ushort
            or int
            or uint
            or long
            or ulong
            or float
            or double
            or decimal
            or char
            or Guid
            or DateTime
            or DateTimeOffset
            or Stream
            or Uri
            or TimeSpan;
}
