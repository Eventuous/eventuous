using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Tests.Subscriptions;

public class HandlingStatusTests {
    [Test]
    public void AckAndNackShouldNack() {
        const EventHandlingStatus actual = EventHandlingStatus.Success | EventHandlingStatus.Failure;
        (actual & EventHandlingStatus.Handled).Should().Be(EventHandlingStatus.Failure);
    }

    [Test]
    public void AckAndIgnoreShouldAck() {
        const EventHandlingStatus actual = EventHandlingStatus.Success | EventHandlingStatus.Ignored;
        (actual & EventHandlingStatus.Handled).Should().Be(EventHandlingStatus.Success);
    }

    [Test]
    public void NackAndIgnoreShouldNack() {
        const EventHandlingStatus actual = EventHandlingStatus.Failure | EventHandlingStatus.Ignored;
        (actual & EventHandlingStatus.Handled).Should().Be(EventHandlingStatus.Failure);
    }

    [Test]
    public void PendingShouldBeHandled() {
        const EventHandlingStatus actual = EventHandlingStatus.Pending;
        (actual & EventHandlingStatus.Handled).Should().NotBe(EventHandlingStatus.Failure);
        (actual & EventHandlingStatus.Handled).Should().NotBe(EventHandlingStatus.Ignored);
    }

    [Test]
    public void IgnoredShouldBeIgnored() {
        const EventHandlingStatus actual = EventHandlingStatus.Ignored;
        (actual & EventHandlingStatus.Handled).Should().Be(0);
    }

    [Test]
    public void NackAndIgnoreShouldFail() {
        var context = TestContext.CreateContext();
        context.Nack<object>(new());
        context.Ignore("test");
        context.HasFailed().Should().BeTrue();
        context.WasIgnored().Should().BeFalse();
        context.HandlingResults.IsPending().Should().BeFalse();
    }

    [Test]
    public void NackAckAndIgnoreShouldFail() {
        var context = TestContext.CreateContext();
        context.Nack<object>(new());
        context.Ack<int>();
        context.Ignore<long>();
        context.HasFailed().Should().BeTrue();
        context.WasIgnored().Should().BeFalse();
        context.HandlingResults.IsPending().Should().BeFalse();
    }

    [Test]
    public void AckAndIgnoreShouldSucceed() {
        var context = TestContext.CreateContext();
        context.Ack<object>();
        context.Ignore<int>();
        context.HasFailed().Should().BeFalse();
        context.WasIgnored().Should().BeFalse();
        context.HandlingResults.IsPending().Should().BeFalse();
    }

    [Test]
    public void IgnoreAndIgnoreShouldIgnore() {
        var context = TestContext.CreateContext();
        context.Ignore<object>();
        context.Ignore<int>();
        context.WasIgnored().Should().BeTrue();
        context.HandlingResults.IsPending().Should().BeFalse();
    }

    [Test]
    public void PendingShouldBePending() {
        var context = TestContext.CreateContext();
        context.WasIgnored().Should().BeFalse();
        context.HasFailed().Should().BeFalse();
        context.HandlingResults.IsPending().Should().BeTrue();
    }
}
