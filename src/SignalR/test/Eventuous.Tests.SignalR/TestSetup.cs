using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Eventuous.Tests.SignalR;

static class TestSetup {
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255")]
    internal static void Initialize()
        => EventSerializer.SetDefault(new DefaultEventSerializer(new(JsonSerializerDefaults.Web)));
}
