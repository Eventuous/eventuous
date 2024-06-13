using MongoDb.Bson.NodaTime;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace Bookings.Infrastructure;

public static class Mongo {
    public static IMongoDatabase ConfigureMongo(IConfiguration configuration) {
        NodaTimeSerializers.Register();
        var config = configuration.GetSection("Mongo").Get<MongoSettings>();

        var settings = MongoClientSettings.FromConnectionString(config.ConnectionString);

        if (config.User != null && config.Password != null) {
            settings.Credential = new MongoCredential(
                null,
                new MongoInternalIdentity("admin", config.User),
                new PasswordEvidence(config.Password)
            );
        }

        settings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());
        return new MongoClient(settings).GetDatabase(config.Database);
    }

    public record MongoSettings {
        public string  ConnectionString { get; init; } = null!;
        public string  Database         { get; init; } = null!;
        public string? User             { get; init; }
        public string? Password         { get; init; }
    }
}
