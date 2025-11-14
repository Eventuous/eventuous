EXEC tSQLt.NewTestClass 'truncate_stream';
GO

CREATE PROCEDURE truncate_stream.Setup
AS
BEGIN
    EXEC tSQLt.FakeTable 'eventuous.Streams', @Identity = 1;
    EXEC tSQLt.FakeTable 'eventuous.Messages', @Identity = 1;
END;
GO

CREATE PROCEDURE truncate_stream.[Test happy path to truncate a stream]
AS
BEGIN
    DECLARE
        @stream_name_1     VARCHAR(850) = 'Receipt-1',
        @stream_name_2     VARCHAR(850) = 'Receipt-2',
        @expected_version INT = 3,
        @position         INT = 2; -- keep messages at this position and greater than this position

    INSERT eventuous.Streams (StreamName, Version)
    VALUES
        (@stream_name_1, @expected_version),
        (@stream_name_2, 8);

    INSERT INTO eventuous.Messages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
    VALUES
        (NEWID(), N'whatever', 1, 0, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 1, 1, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 1, 2, N'{}', N'{}', SYSUTCDATETIME());

    INSERT INTO eventuous.Messages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
    VALUES
        (NEWID(), N'whatever', 2, 0, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 1, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 2, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 3, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 4, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 5, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 6, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 7, N'{}', N'{}', SYSUTCDATETIME());

    EXEC eventuous.truncate_stream
        @stream_name      = @stream_name_1,
        @expected_version = @expected_version,
        @position         = 2;

    /* Assert Messages */
    SELECT StreamId,
           StreamPosition
    INTO #ActualMessages
    FROM eventuous.Messages;

    SELECT TOP (0) *
    INTO #ExpectedMessages
    FROM #ActualMessages;

    INSERT INTO #ExpectedMessages (StreamId, StreamPosition)
    VALUES
    --(1, 0), was truncated ✔️
    --(1, 1), was truncated ✔️
      (1, 2),
      (2, 0),
      (2, 1),
      (2, 2),
      (2, 3),
      (2, 4),
      (2, 5),
      (2, 6),
      (2, 7);

    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedMessages', @Actual = N'#ActualMessages';

END;
GO

CREATE PROCEDURE truncate_stream.[Test wrong expected version ExpectException]
AS
BEGIN
    DECLARE
        @stream_name_1     VARCHAR(850) = 'Receipt-1',
        @stream_name_2     VARCHAR(850) = 'Receipt-2',
        @current_version  INT = 3,
        @expected_version INT = 8,
        @position         INT = 2;

    INSERT eventuous.Streams (StreamName, Version)
    VALUES
        (@stream_name_1, @current_version),
        (@stream_name_2, 8);

    INSERT INTO eventuous.Messages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
    VALUES
        (NEWID(), N'whatever', 1, 0, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 1, 1, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 1, 2, N'{}', N'{}', SYSUTCDATETIME());

    INSERT INTO eventuous.Messages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
    VALUES
        (NEWID(), N'whatever', 2, 0, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 1, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 2, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 3, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 4, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 5, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 6, N'{}', N'{}', SYSUTCDATETIME()),
        (NEWID(), N'whatever', 2, 7, N'{}', N'{}', SYSUTCDATETIME());

    DECLARE @ExpectedMessage VARCHAR(256) = CONCAT('WrongExpectedVersion ', @expected_version, ', current version ', @current_version)
    EXEC tSQLt.ExpectException
        @ExpectedMessage  = @ExpectedMessage,
        @ExpectedSeverity = NULL,
        @ExpectedState    = NULL;
    EXEC eventuous.truncate_stream
        @stream_name      = @stream_name_1,
        @expected_version = @expected_version,
        @position         = 2;

END;
GO

CREATE PROCEDURE truncate_stream.[Test stream not found ExpectException]
AS
BEGIN

    DECLARE @ExpectedMessage VARCHAR(256) = 'StreamNotFound'
    EXEC tSQLt.ExpectException
        @ExpectedMessage  = @ExpectedMessage,
        @ExpectedSeverity = NULL,
        @ExpectedState    = NULL;
    EXEC eventuous.truncate_stream
        @stream_name      = 'Unknown',
        @expected_version = 561,
        @position         = 87;

END;
GO

-- EXEC tsqlt.RunTestClass 'truncate_stream' -- Don't check in