EXEC tSQLt.NewTestClass 'read_stream_forwards';
GO

CREATE PROCEDURE read_stream_forwards.Setup
AS
BEGIN
    EXEC tSQLt.FakeTable 'eventuous.Streams';
    EXEC tSQLt.FakeTable 'eventuous.Messages', @Identity = 1;
END;
GO

CREATE PROCEDURE read_stream_forwards.[Test happy path reading forwards]
AS
BEGIN

    DECLARE
        @streamName_1 NVARCHAR (850) = 'Receipt-1',
        @streamId_1 INT = 1,
        @streamName_2 NVARCHAR (850) = 'Bin-8',
        @streamId_2 INT = 2;

    INSERT INTO eventuous.Streams (StreamId, StreamName, [Version])
    VALUES
        (@streamId_1, @streamName_1, 1),
        (@streamId_2, @streamName_2, 1);

    INSERT INTO eventuous.Messages (MessageType, StreamId, StreamPosition)
    VALUES
        ('First',  @streamId_1, 0),
        ('First',  @streamId_2, 0),
        ('Second', @streamId_2, 1),
        ('Second', @streamId_1, 1);

    CREATE TABLE #ProcResult (
        MessageId       UNIQUEIDENTIFIER,
        MessageType     NVARCHAR(128),
        StreamPosition  INT,
        GlobalPosition  BIGINT,
        JsonData        NVARCHAR(MAX),
        JsonMetadata    NVARCHAR(MAX),
        Created         DATETIME2(7)
    );

    -- Starting at 1 and getting 2 Messages means we will only get 1 eventuous.Messages that we inserted above. Assert that.
    DECLARE
        @stream_name   NVARCHAR(850) = 'Bin-8',
        @from_position BIGINT = 1,
        @count         INT    = 2;

    INSERT INTO #ProcResult
    EXEC eventuous.read_stream_forwards
        @stream_name   = @stream_name,
        @from_position = @from_position,
        @count         = @count;

    SELECT
        --MessageId, not asserting the GUID
        MessageType,
        StreamPosition,
        GlobalPosition
        -- Created, -- not asserting the SYSUTCDATETIME()
    INTO #ActualResult
    FROM #ProcResult;

    SELECT TOP (0) *
    INTO #ExpectedResult
    FROM #ActualResult;

    -- we will only get 1 eventuous.Messages that we inserted above
    INSERT INTO #ExpectedResult (MessageType, StreamPosition, GlobalPosition)
    VALUES
        ('Second', 1, 2);

    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedResult',  @Actual = N'#ActualResult';

END;
GO

-- EXEC tsqlt.RunTestClass 'read_stream_forwards' -- Don't check in