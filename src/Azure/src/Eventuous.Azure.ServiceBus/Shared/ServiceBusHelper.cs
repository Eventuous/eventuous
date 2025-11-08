using System;
using System.IO;

namespace Eventuous.Azure.ServiceBus.Shared;

public static class ServiceBusHelper
{
 public static bool IsSerialisableByServiceBus(object? value) => 
    value is not null && (
        value is string ||
        value is bool ||
        value is byte ||
        value is sbyte ||
        value is short ||
        value is ushort ||
        value is int ||
        value is uint ||
        value is long ||
        value is ulong ||
        value is float ||
        value is double ||
        value is decimal ||
        value is char ||
        value is Guid ||
        value is DateTime ||
        value is DateTimeOffset ||
        value is Stream ||
        value is Uri ||
        value is TimeSpan
    );
}
