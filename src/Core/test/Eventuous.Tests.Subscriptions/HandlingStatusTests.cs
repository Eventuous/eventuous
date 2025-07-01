using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

public class HandlingStatusTests {
    [Test]
    public void AckAndNackShouldNack() {
        const EventHandlingStatus actual = EventHandlingStatus.Success | EventHandlingStatus.Failure;
        (actual & EventHandlingStatus.Handled).ShouldBe(EventHandlingStatus.Failure);
    }

    [Test]
    public void AckAndIgnoreShouldAck() {
        const EventHandlingStatus actual = EventHandlingStatus.Success | EventHandlingStatus.Ignored;
        (actual & EventHandlingStatus.Handled).ShouldBe(EventHandlingStatus.Success);
    }

    [Test]
    public void NackAndIgnoreShouldNack() {
        const EventHandlingStatus actual = EventHandlingStatus.Failure | EventHandlingStatus.Ignored;
        (actual & EventHandlingStatus.Handled).ShouldBe(EventHandlingStatus.Failure);
    }

    [Test]
    public void PendingShouldBeHandled() {
        const EventHandlingStatus actual = EventHandlingStatus.Pending;
        (actual & EventHandlingStatus.Handled).ShouldNotBe(EventHandlingStatus.Failure);
        (actual & EventHandlingStatus.Handled).ShouldNotBe(EventHandlingStatus.Ignored);
    }

    [Test]
    public void IgnoredShouldBeIgnored() {
        const EventHandlingStatus actual = EventHandlingStatus.Ignored;
        ((int)(actual & EventHandlingStatus.Handled)).ShouldBe(0);
    }

    [Test]
    public void NackAndIgnoreShouldFail() {
        var context = TestContext.CreateContext();
        context.Nack<object>(new());
        context.Ignore("test");
        context.HasFailed().ShouldBeTrue();
        context.WasIgnored().ShouldBeFalse();
        context.HandlingResults.IsPending().ShouldBeFalse();
    }

    [Test]
    public void NackAckAndIgnoreShouldFail() {
        var context = TestContext.CreateContext();
        context.Nack<object>(new());
        context.Ack<int>();
        context.Ignore<long>();
        context.HasFailed().ShouldBeTrue();
        context.WasIgnored().ShouldBeFalse();
        context.HandlingResults.IsPending().ShouldBeFalse();
    }

    [Test]
    public void AckAndIgnoreShouldSucceed() {
        var context = TestContext.CreateContext();
        context.Ack<object>();
        context.Ignore<int>();
        context.HasFailed().ShouldBeFalse();
        context.WasIgnored().ShouldBeFalse();
        context.HandlingResults.IsPending().ShouldBeFalse();
    }

    [Test]
    public void IgnoreAndIgnoreShouldIgnore() {
        var context = TestContext.CreateContext();
        context.Ignore<object>();
        context.Ignore<int>();
        context.WasIgnored().ShouldBeTrue();
        context.HandlingResults.IsPending().ShouldBeFalse();
    }

    [Test]
    public void PendingShouldBePending() {
        var context = TestContext.CreateContext();
        context.WasIgnored().ShouldBeFalse();
        context.HasFailed().ShouldBeFalse();
        context.HandlingResults.IsPending().ShouldBeTrue();
    }
}
