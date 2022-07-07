// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.Tests.Postgres.Store.Helpers;

namespace Eventuous.Tests.Postgres.Store;

public class AppendEvents {
    [Fact]
    public async Task ShouldAppendToNoStream() {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        var result     = await AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        result.NextExpectedVersion.Should().Be(0);
    }

    [Fact]
    public async Task ShouldAppendOneByOne() {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        var result = await AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = CreateEvent();

        var version = new ExpectedStreamVersion(result.NextExpectedVersion);
        result = await AppendEvent(stream, evt, version);

        result.NextExpectedVersion.Should().Be(1);
    }

    [Fact]
    public async Task ShouldFailOnWrongVersionNoStream() {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        await AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);
        
        evt = CreateEvent();

        var task = () => AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);
        await task.Should().ThrowAsync<AppendToStreamException>();
    }

    [Fact]
    public async Task ShouldFailOnWrongVersion() {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        await AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);
        
        evt = CreateEvent();

        var task = () => AppendEvent(stream, evt, new ExpectedStreamVersion(3));
        await task.Should().ThrowAsync<AppendToStreamException>();
    }

}
