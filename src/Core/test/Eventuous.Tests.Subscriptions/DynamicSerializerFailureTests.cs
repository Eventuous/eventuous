using System.Text.Json;
using static Eventuous.DeserializationResult;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// The reflection-based serializer shares its failure results like the static one; the subscription project already
/// references it, so it's pinned here.
/// </summary>
public class DynamicSerializerFailureTests {
    static DefaultEventSerializer CreateSerializer(ITypeMapper typeMapper) => new(new(JsonSerializerDefaults.Web), typeMapper);

    static TypeMapper MapperWithTestEvent() {
        var typeMapper = new TypeMapper();
        typeMapper.AddType<TestEvent>("dynamic-failure-test-event");

        return typeMapper;
    }

    [Test]
    public async Task ShouldReuseUnknownTypeFailure() {
        var serializer = CreateSerializer(new TypeMapper());

        var first  = serializer.DeserializeEvent("{}"u8, "unregistered", "application/json");
        var second = serializer.DeserializeEvent("{}"u8, "other-unregistered", "application/json");

        await Assert.That(((FailedToDeserialize)first).Error).IsEqualTo(DeserializationError.UnknownType);
        await Assert.That(second).IsSameReferenceAs(first);
    }

    [Test]
    public async Task ShouldReuseContentTypeMismatchFailure() {
        var serializer = CreateSerializer(MapperWithTestEvent());

        var first  = serializer.DeserializeEvent("{}"u8, "dynamic-failure-test-event", "application/xml");
        var second = serializer.DeserializeEvent("{}"u8, "dynamic-failure-test-event", "text/plain");

        await Assert.That(((FailedToDeserialize)first).Error).IsEqualTo(DeserializationError.ContentTypeMismatch);
        await Assert.That(second).IsSameReferenceAs(first);
    }

    [Test]
    public async Task ShouldReusePayloadEmptyFailure() {
        var serializer = CreateSerializer(MapperWithTestEvent());

        var first  = serializer.DeserializeEvent("null"u8, "dynamic-failure-test-event", "application/json");
        var second = serializer.DeserializeEvent("null"u8, "dynamic-failure-test-event", "application/json");

        await Assert.That(((FailedToDeserialize)first).Error).IsEqualTo(DeserializationError.PayloadEmpty);
        await Assert.That(second).IsSameReferenceAs(first);
    }

    record TestEvent(string Name);
}
