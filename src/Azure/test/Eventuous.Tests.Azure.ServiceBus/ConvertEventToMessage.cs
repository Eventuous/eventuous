using Eventuous.Azure.ServiceBus.Producers;
using Eventuous.Azure.ServiceBus.Shared;

namespace Eventuous.Tests.Azure.ServiceBus;

public class ConvertEventToMessage {
    readonly Guid              _messageId = Guid.NewGuid();
    readonly ServiceBusMessage _message;

    public ConvertEventToMessage() {
        var builder = new ServiceBusMessageBuilder(
            DefaultEventSerializer.Instance,
            "test-stream",
            new(),
            new() {
                Subject    = "test-subject",
                To         = "test-to",
                ReplyTo    = "test-reply-to",
                TimeToLive = TimeSpan.FromMinutes(5)
            }
        );

        _message = builder.CreateServiceBusMessage(
            new(
                new SomeEvent {
                    Id   = "event-id",
                    Name = "Test Event"
                },
                new() {
                    [MetaTags.CorrelationId] = "correlation-id",
                    [MetaTags.CausationId]   = "causation-id",
                    ["AAA"]                  = 1111
                },
                new() { ["BBB"] = "12345" },
                _messageId
            )
        );
    }

    [Test]
    public async Task ContentType() {
        await Assert.That(_message.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task MessageId() {
        await Assert.That(_message.MessageId).IsEqualTo(_messageId.ToString());
    }

    [Test]
    public async Task Subject() {
        await Assert.That(_message.Subject).IsEqualTo("test-subject");
    }

    [Test]
    public async Task To() {
        await Assert.That(_message.To).IsEqualTo("test-to");
    }

    [Test]
    public async Task ReplyTo() {
        await Assert.That(_message.ReplyTo).IsEqualTo("test-reply-to");
    }

    [Test]
    public async Task TimeToLive() {
        await Assert.That(_message.TimeToLive).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task CorrelationId() {
        await Assert.That(_message.CorrelationId).IsEqualTo("correlation-id");
    }

    [Test]
    [Arguments("MessageId")]
    [Arguments("CorrelationId")]
    public async Task ApplicationPropertiesHasNoReservedAttributes(string propertyName) {
        await Assert.That(_message.ApplicationProperties.ContainsKey(propertyName)).IsFalse();
    }

    [Test]
    [Arguments(MetaTags.CausationId, "causation-id")]
    [Arguments("AAA", 1111)]
    [Arguments("BBB", "12345")]
    public async Task ApplicationPropertiesHasAttributes(string propertyName, object expectedValue) {
        await Assert.That(_message.ApplicationProperties[propertyName]).IsEqualTo(expectedValue);
    }

    public class WithMessagePropertiesInMetaData {
        readonly ServiceBusMessage _message;

        public WithMessagePropertiesInMetaData() {
            var attributeNames = new ServiceBusMessageAttributeNames();
            var builder        = new ServiceBusMessageBuilder(DefaultEventSerializer.Instance, "test-stream", attributeNames, new());

            _message = builder.CreateServiceBusMessage(
                new(
                    new SomeEvent {
                        Id   = "event-id",
                        Name = "Test Event"
                    },
                    new() {
                        [attributeNames.MessageId]     = "12345",
                        [attributeNames.CorrelationId] = "correlation-id",
                        [attributeNames.CausationId]   = "causation-id",
                        [attributeNames.ReplyTo]       = "test-reply-to",
                        [attributeNames.Subject]       = "test-subject",
                        [attributeNames.To]            = "test-to"
                    },
                    new()
                )
            );
        }

        [Test]
        public async Task MessageId() {
            await Assert.That(_message.MessageId).IsEqualTo("12345");
        }

        [Test]
        public async Task Subject() {
            await Assert.That(_message.Subject).IsEqualTo("test-subject");
        }

        [Test]
        public async Task To() {
            await Assert.That(_message.To).IsEqualTo("test-to");
        }

        [Test]
        public async Task ReplyTo() {
            await Assert.That(_message.ReplyTo).IsEqualTo("test-reply-to");
        }

        [Test]
        public async Task CorrelationId() {
            await Assert.That(_message.CorrelationId).IsEqualTo("correlation-id");
        }
    }
}
