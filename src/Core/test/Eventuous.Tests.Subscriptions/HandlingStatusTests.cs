using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Tests.Subscriptions; 

public class HandlingStatusTests {
    Fixture Fixture { get; } = new();
    
    [Fact]
    public void AckAndNackShouldNack() {
        const EventHandlingStatus actual = EventHandlingStatus.Success | EventHandlingStatus.Failure;
        (actual & EventHandlingStatus.Handled).Should().Be(EventHandlingStatus.Failure);
    }

    [Fact]
    public void AckAndIgnoreShouldAck() {
        const EventHandlingStatus actual = EventHandlingStatus.Success | EventHandlingStatus.Ignored;
        (actual & EventHandlingStatus.Handled).Should().Be(EventHandlingStatus.Success);
    }
    
    [Fact]
    public void NackAndIgnoreShouldNack() {
        const EventHandlingStatus actual = EventHandlingStatus.Failure | EventHandlingStatus.Ignored;
        (actual & EventHandlingStatus.Handled).Should().Be(EventHandlingStatus.Failure);
    }

    [Fact]
    public void PendingShouldBeHandled() {
        const EventHandlingStatus actual = EventHandlingStatus.Pending;
        (actual & EventHandlingStatus.Handled).Should().NotBe(EventHandlingStatus.Failure);
        (actual & EventHandlingStatus.Handled).Should().NotBe(EventHandlingStatus.Ignored);
    }

    [Fact]
    public void IgnoredShouldBeIgnored() {
        const EventHandlingStatus actual = EventHandlingStatus.Ignored;
        (actual & EventHandlingStatus.Handled).Should().Be(0);
    }

    [Fact]
    public void NackAndIgnoreShouldFail() {
        var context = Fixture.Create<MessageConsumeContext>();
        context.Nack<object>(new Exception());
        context.Ignore("test");
        context.HasFailed().Should().BeTrue();
        context.WasIgnored().Should().BeFalse();
    }

    [Fact]
    public void NackAckAndIgnoreShouldFail() {
        var context = Fixture.Create<MessageConsumeContext>();
        context.Nack<object>(new Exception());
        context.Ack<int>();
        context.Ignore<long>();
        context.HasFailed().Should().BeTrue();
        context.WasIgnored().Should().BeFalse();
    }

    [Fact]
    public void AckAndIgnoreShouldSucceed() {
        var context = Fixture.Create<MessageConsumeContext>();
        context.Ack<object>();
        context.Ignore<int>();
        context.HasFailed().Should().BeFalse();
        context.WasIgnored().Should().BeFalse();
    }

    [Fact]
    public void IgnoreAndIgnoreShouldIgnore() {
        var context = Fixture.Create<MessageConsumeContext>();
        context.Ignore<object>();
        context.Ignore<int>();
        context.WasIgnored().Should().BeTrue();
    }
}