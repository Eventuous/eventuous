// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Tests.Postgres.Fixtures;
using static Eventuous.Sut.Domain.BookingEvents;
using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Postgres.StoreTests;

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

    static StreamName GetStreamName() => new(Instance.Auto.Create<string>());

    static BookingImported CreateEvent() {
        var cmd = DomainFixture.CreateImportBooking();
        return new BookingImported(cmd.BookingId, cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);
    }

    static Task<AppendEventsResult> AppendEvent(StreamName stream, object evt, ExpectedStreamVersion version) {
        var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", 0);
        return Instance.EventStore.AppendEvents(stream, version, new[] { streamEvent }, default);
    }
}
