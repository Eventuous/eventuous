// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class StoreAppendTests<T> : IClassFixture<T> where T : StoreFixtureBase {
    readonly T _fixture;

    protected StoreAppendTests(T fixture) {
        fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldAppendToNoStream() {
        var evt        = _fixture.CreateEvent();
        var streamName = _fixture.GetStreamName();
        var result     = await _fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        result.NextExpectedVersion.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldAppendOneByOne() {
        var evt    = _fixture.CreateEvent();
        var stream = _fixture.GetStreamName();

        var result = await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = _fixture.CreateEvent();

        var version = new ExpectedStreamVersion(result.NextExpectedVersion);
        result = await _fixture.AppendEvent(stream, evt, version);

        result.NextExpectedVersion.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldFailOnWrongVersionNoStream() {
        var evt    = _fixture.CreateEvent();
        var stream = _fixture.GetStreamName();

        await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = _fixture.CreateEvent();

        var task = () => _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);
        await task.Should().ThrowAsync<AppendToStreamException>();
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldFailOnWrongVersion() {
        var evt    = _fixture.CreateEvent();
        var stream = _fixture.GetStreamName();

        await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = _fixture.CreateEvent();

        var task = () => _fixture.AppendEvent(stream, evt, new(3));
        await task.Should().ThrowAsync<AppendToStreamException>();
    }
}
