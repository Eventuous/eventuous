using System.Diagnostics;
using Eventuous.Diagnostics;

namespace Eventuous.Tests.Diagnostics;

public class ActivityHelpersTests {
    [Test]
    public async Task ShouldShareTheOkStatusWithoutDescription() {
        var first  = ActivityStatus.Ok();
        var second = ActivityStatus.Ok();

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(first.StatusCode).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(first.Description).IsNull();
        await Assert.That(first.Exception).IsNull();
    }

    [Test]
    public async Task ShouldKeepTheDescriptionOfAnOkStatus() {
        var status = ActivityStatus.Ok("done");

        await Assert.That(status.StatusCode).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(status.Description).IsEqualTo("done");
    }

    [Test]
    public async Task ShouldCopyOnlyStringTagsFromTheParent() {
        var previous = Activity.Current;

        try {
            using var parent = new Activity("parent").Start();
            parent.SetTag("text", "value");
            parent.SetTag("number", 42);

            using var child = new Activity("child").Start();

            child.CopyParentTag("text").CopyParentTag("number").CopyParentTag("missing");
            child.SetOrCopyParentTag("renamed", null, "text");
            child.SetOrCopyParentTag("explicit", "own", "text");

            await Assert.That(child.GetTagItem("text")).IsEqualTo("value");
            await Assert.That(child.GetTagItem("number")).IsNull();
            await Assert.That(child.GetTagItem("missing")).IsNull();
            await Assert.That(child.GetTagItem("renamed")).IsEqualTo("value");
            await Assert.That(child.GetTagItem("explicit")).IsEqualTo("own");
        } finally {
            Activity.Current = previous;
        }
    }
}
