using Eventuous.Postgresql;

namespace Eventuous.Tests.Postgres.Subscriptions;

/// <summary>
/// The queries are read on every poll, append and read, so the schema builds their text once rather than per access.
/// </summary>
public class SchemaSqlTests {
    [Test]
    public async Task Queries_are_built_once_for_the_schema() {
        var schema = new Schema("custom_schema");

        await Assert.That(schema.ReadAllForwards).IsEqualTo("select * from custom_schema.read_all_forwards(@_from_position, @_count)");
        await Assert.That(schema.ReadStreamSub).IsEqualTo("select * from custom_schema.read_stream_sub(@_stream_id, @_stream_name, @_from_position, @_count)");
        await Assert.That(schema.AppendEvents).IsEqualTo("select * from custom_schema.append_events(@_stream_name, @_expected_version, @_created, @_messages)");

        await Assert.That(schema.StreamMessage).IsSameReferenceAs(schema.StreamMessage);
        await Assert.That(schema.AppendEvents).IsSameReferenceAs(schema.AppendEvents);
        await Assert.That(schema.ReadStreamForwards).IsSameReferenceAs(schema.ReadStreamForwards);
        await Assert.That(schema.ReadStreamBackwards).IsSameReferenceAs(schema.ReadStreamBackwards);
        await Assert.That(schema.ReadStreamSub).IsSameReferenceAs(schema.ReadStreamSub);
        await Assert.That(schema.ReadAllForwards).IsSameReferenceAs(schema.ReadAllForwards);
        await Assert.That(schema.CheckStream).IsSameReferenceAs(schema.CheckStream);
        await Assert.That(schema.StreamExists).IsSameReferenceAs(schema.StreamExists);
        await Assert.That(schema.TruncateStream).IsSameReferenceAs(schema.TruncateStream);
        await Assert.That(schema.GetCheckpointSql).IsSameReferenceAs(schema.GetCheckpointSql);
        await Assert.That(schema.AddCheckpointSql).IsSameReferenceAs(schema.AddCheckpointSql);
        await Assert.That(schema.UpdateCheckpointSql).IsSameReferenceAs(schema.UpdateCheckpointSql);
        await Assert.That(schema.TryInsertTombstone).IsSameReferenceAs(schema.TryInsertTombstone);
    }
}
