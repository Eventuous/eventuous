EXEC tSQLt.NewTestClass 'append_events';
GO

CREATE PROCEDURE append_events.Setup
AS
BEGIN
    EXEC tSQLt.FakeTable 'eventuous.Streams', @Identity = 1;
    EXEC tSQLt.FakeTable 'eventuous.Messages', @Identity = 1;
END;
GO

CREATE PROCEDURE append_events.[Test single message]
AS
BEGIN
    DECLARE
        @stream_name      VARCHAR(850)     = 'Receipt-1',
        @expected_version INT              = -1, -- NoStream
        @created          DATETIME2(7)     = SYSUTCDATETIME(),
        @messages         eventuous.StreamMessage;

    INSERT INTO @messages (message_id, message_type, json_data, json_metadata)
    VALUES
        (
            NEWID(),
            N'V1.Receipt.Started',
            N'{"userId":201}', N'{"trace-id":"f685468b5c11308e025ef8fbb88b2719","span-id":"46fdb35780d6a129","parent-span-id":"88859bf9294597b6"}'
        );

    EXEC eventuous.append_events
        @stream_name      = @stream_name,
        @expected_version = @expected_version,
        @created          = @created,
        @messages         = @messages;

    SELECT StreamName,
           Version
    INTO #ActualStreams
    FROM eventuous.Streams;

    SELECT TOP (0) *
    INTO #ExpectedStreams
    FROM #ActualStreams;

    INSERT INTO #ExpectedStreams (StreamName, Version)
    VALUES (@stream_name, 0);

    SELECT GlobalPosition,
           MessageId,
           MessageType,
           StreamId,
           StreamPosition,
           JsonData,
           JsonMetadata,
           Created
    INTO #ActualMessages
    FROM eventuous.Messages;

    SELECT TOP (0) *
    INTO #ExpectedMessages
    FROM #ActualMessages;

    INSERT INTO #ExpectedMessages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
    SELECT
        message_id,
        message_type,
        1,
        0,
        json_data,
        json_metadata,
        @created
    FROM @messages;

    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedStreams',  @Actual = N'#ActualStreams';
    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedMessages', @Actual = N'#ActualMessages';

END;
GO

CREATE PROCEDURE append_events.[Test multiple messages]
AS
BEGIN

    DECLARE
        @stream_name      VARCHAR(850)     = 'Receipt-1',
        @expected_version INT              = -1, -- NoStream
        @created          DATETIME2(7)     = SYSUTCDATETIME(),
        @messages         eventuous.StreamMessage;

    INSERT INTO @messages (message_id, message_type, json_data, json_metadata)
    VALUES 
        (
            NEWID(),
            N'V1.Receipt.Started',
            N'{"userId":201}', 
            N'{"trace-id":"f685468b5c11308e025ef8fbb88b2719","span-id":"46fdb35780d6a129","parent-span-id":"88859bf9294597b6"}'
        ),

        (
            NEWID(),
            N'V1.Receipt.LineAdjusted',
            N'{"userId":201,"itemId":8,"quantity":20,"binDescription":"CheckIn"}', 
            N'{"trace-id":"3cd6a40c9a41735df627244506a9c6b3","span-id":"0497917daefb11e4","parent-span-id":"194f872000aacabe"}'
        );

    EXEC eventuous.append_events
        @stream_name      = @stream_name,
        @expected_version = @expected_version,
        @created          = @created,
        @messages         = @messages;

    /* Assert Streams */
    SELECT StreamName,
           [Version]
    INTO #ActualStreams
    FROM eventuous.Streams;

    SELECT TOP (0) *
    INTO #ExpectedStreams
    FROM #ActualStreams;

    INSERT INTO #ExpectedStreams (StreamName, [Version])
    VALUES (@stream_name, 1);

    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedStreams',  @Actual = N'#ActualStreams';

    /* Assert Messages */
    SELECT GlobalPosition,
           MessageId,
           MessageType,
           StreamId,
           StreamPosition,
           JsonData,
           JsonMetadata,
           Created
    INTO #ActualMessages
    FROM eventuous.Messages;

    SELECT TOP (0) *
    INTO #ExpectedMessages
    FROM #ActualMessages;

    INSERT INTO #ExpectedMessages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
    SELECT
        message_id,
        message_type,
        1,
        0, -- 0
        json_data,
        json_metadata,
        @created
    FROM @messages
    WHERE message_type = 'V1.Receipt.Started';

    INSERT INTO #ExpectedMessages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
    SELECT
        message_id,
        message_type,
        1,
        1, -- 1
        json_data,
        json_metadata,
        @created
    FROM @messages
    WHERE message_type = 'V1.Receipt.LineAdjusted';

    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedMessages', @Actual = N'#ActualMessages';

END;
GO
-- EXEC tsqlt.RunTestClass 'append_events' -- Don't check in
