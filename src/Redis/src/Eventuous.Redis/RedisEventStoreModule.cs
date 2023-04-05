using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Eventuous.Tools;

namespace Eventuous.Redis;

public static class RedisEventStoreModule {
    static readonly Assembly Assembly = typeof(RedisEventStoreModule).Assembly;

    public static async Task LoadModule(GetRedisDatabase getDatabase) {
        var names = Assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".lua"))
            .OrderBy(x => x);

        var db = getDatabase();

        foreach (var name in names) {
            await using var stream = Assembly.GetManifestResourceStream(name);
            using var       reader = new StreamReader(stream!);
            var             script = await reader.ReadToEndAsync().NoContext();

            try {
                await db.ExecuteAsync("FUNCTION", "LOAD", "REPLACE", script);
            }
            catch (Exception e) {
                if (!e.Message.Contains("'append_events' already exists")) throw;
            }
        }
    }
}
