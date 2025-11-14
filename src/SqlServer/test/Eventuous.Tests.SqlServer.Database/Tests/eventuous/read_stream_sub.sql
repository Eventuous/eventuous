EXEC tSQLt.NewTestClass 'read_stream_sub';
GO

CREATE PROCEDURE read_stream_sub.Setup
AS
BEGIN
    EXEC tSQLt.FakeTable 'eventuous.Streams';
    -- Preserve identity so GlobalPosition auto-increments from the real seed (0)
    EXEC tSQLt.FakeTable 'eventuous.Messages', @Identity = 1;
END;
GO

CREATE PROCEDURE read_stream_sub.[Test happy path subscription window]
AS
BEGIN
    -- Arrange
    DECLARE
        @streamName_1 NVARCHAR (850) = 'Receipt-1',
        @streamId_1   INT            = 1,
        @streamName_2 NVARCHAR (850) = 'Bin-8',
        @streamId_2   INT            = 2;

    INSERT INTO eventuous.Streams (StreamId, StreamName, [Version])
    VALUES
        (@streamId_1, @streamName_1, 1),
        (@streamId_2, @streamName_2, 1);

    /*
        Insert order determines GlobalPosition (seed 0):
          GlobalPosition=0: (First,  @streamId_1, pos 0)
          GlobalPosition=1: (First,  @streamId_2, pos 0)
          GlobalPosition=2: (Second, @streamId_2, pos 1)
          GlobalPosition=3: (Second, @streamId_1, pos 1)
    */
    INSERT INTO eventuous.Messages (MessageType, StreamId, StreamPosition)
    VALUES
        ('First',  @streamId_1, 0),
        ('First',  @streamId_2, 0),
        ('Second', @streamId_2, 1),
        ('Second', @streamId_1, 1);

    CREATE TABLE #ProcResult
    (
        MessageId      UNIQUEIDENTIFIER,
        MessageType    NVARCHAR(128),
        StreamPosition INT,
        GlobalPosition BIGINT,
        JsonData       NVARCHAR(MAX),
        JsonMetadata   NVARCHAR(MAX),
        Created        DATETIME2(7),
        StreamName     NVARCHAR(850)
    );

    DECLARE
        @stream_id     INT          = @streamId_1,
        @stream_name   NVARCHAR(850)= @streamName_1,
        @from_position INT          = 0,
        @count         INT          = 2;

    -- Act
    INSERT INTO #ProcResult
    EXEC eventuous.read_stream_sub
        @stream_id     = @stream_id,
        @stream_name   = @stream_name,
        @from_position = @from_position,
        @count         = @count;

    -- Project only the columns we want to assert
    SELECT
        MessageType,
        StreamPosition,
        GlobalPosition,
        StreamName
    INTO #ActualResult
    FROM #ProcResult;

    SELECT TOP (0) *
    INTO #ExpectedResult
    FROM #ActualResult;

    -- Expect the two events from Receipt-1 at positions >= 0, in GlobalPosition order
    INSERT INTO #ExpectedResult (MessageType, StreamPosition, GlobalPosition, StreamName)
    VALUES
        ('First',  0, 0, @stream_name),  -- GlobalPosition 0
        ('Second', 1, 3, @stream_name);  -- GlobalPosition 3

    -- Assert
    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedResult', @Actual = N'#ActualResult';
END;
GO

-- EXEC tsqlt.RunTestClass 'read_stream_sub' -- Don't check in
