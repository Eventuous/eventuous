using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Eventuous.Tests.Subscriptions.Base;

static class TestSetup {
    [ModuleInitializer]
    internal static void Initialize()
        => new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
