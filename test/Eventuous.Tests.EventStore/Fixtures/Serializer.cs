using System.Text.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.EventStore.Fixtures {
    public static class Serializer {
        public static IEventSerializer Json { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );
    }
}