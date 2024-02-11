// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class StoreAppendTests<T>(T fixture) : IClassFixture<T> where T : StoreFixtureBase {
    [Fact]
    public async Task ShouldAppendToNoStream() {
        var evt        = fixture.CreateEvent();
        var streamName = fixture.GetStreamName();
        var result     = await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        result.NextExpectedVersion.Should().Be(0);
    }

    [Fact]
    public async Task ShouldAppendOneByOne() {
        var evt    = fixture.CreateEvent();
        var stream = fixture.GetStreamName();

        var result = await fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = fixture.CreateEvent();

        var version = new ExpectedStreamVersion(result.NextExpectedVersion);
        result = await fixture.AppendEvent(stream, evt, version);

        result.NextExpectedVersion.Should().Be(1);
    }

    [Fact]
    public async Task ShouldFailOnWrongVersionNoStream() {
        var evt    = fixture.CreateEvent();
        var stream = fixture.GetStreamName();

        await fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = fixture.CreateEvent();

        var task = () => fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);
        await task.Should().ThrowAsync<AppendToStreamException>();
    }

    [Fact]
    public async Task ShouldFailOnWrongVersion() {
        var evt    = fixture.CreateEvent();
        var stream = fixture.GetStreamName();

        await fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = fixture.CreateEvent();

        var task = () => fixture.AppendEvent(stream, evt, new ExpectedStreamVersion(3));
        await task.Should().ThrowAsync<AppendToStreamException>();
    }
}
