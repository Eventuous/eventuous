using System.Text.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.TestHelpers;

public static class TestPrimitives {
    public static readonly JsonSerializerOptions DefaultOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForTests();

    public static JsonSerializerOptions ConfigureForTests(this JsonSerializerOptions options)
        => options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
}
