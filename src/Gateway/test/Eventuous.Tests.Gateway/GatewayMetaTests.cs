using Eventuous.Gateway;
using Eventuous.Producers;

namespace Eventuous.Tests.Gateway;

public class GatewayMetaTests {
    [Test]
    public async Task GetOriginalStream_ReturnsStreamName() {
        var streamName = new StreamName("Test-123");

        var headers = new Metadata(new Dictionary<string, object?> {
            [GatewayContextItems.OriginalStream] = streamName
        });

        var message = new ProducedMessage("test", null, headers);

        var result = message.GetOriginalStream();

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(streamName);
    }

    [Test]
    public async Task GetOriginalMessageId_ReturnsMessageId() {
        var messageId = Guid.NewGuid().ToString();

        var headers = new Metadata(new Dictionary<string, object?> {
            [GatewayContextItems.OriginalMessageId] = messageId
        });

        var message = new ProducedMessage("test", null, headers);

        await Assert.That(message.GetOriginalMessageId()).IsEqualTo(messageId);
    }

    [Test]
    public async Task GetOriginalMessageType_ReturnsMessageType() {
        var headers = new Metadata(new Dictionary<string, object?> {
            [GatewayContextItems.OriginalMessageType] = "test-event"
        });

        var message = new ProducedMessage("test", null, headers);

        await Assert.That(message.GetOriginalMessageType()).IsEqualTo("test-event");
    }

    [Test]
    public async Task GetOriginalStreamPosition_ReturnsPosition() {
        var headers = new Metadata(new Dictionary<string, object?> {
            [GatewayContextItems.OriginalStreamPosition] = 42UL
        });

        var message = new ProducedMessage("test", null, headers);

        await Assert.That(message.GetOriginalStreamPosition()).IsEqualTo(42UL);
    }

    [Test]
    public async Task GetOriginalStream_WithNullHeaders_ReturnsNull() {
        var message = new ProducedMessage("test", null);

        await Assert.That(message.GetOriginalStream()).IsNull();
    }
}
