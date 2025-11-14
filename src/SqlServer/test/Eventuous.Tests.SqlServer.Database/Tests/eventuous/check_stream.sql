EXEC tSQLt.NewTestClass 'check_stream';
GO

CREATE PROCEDURE check_stream.Setup
AS
BEGIN
    EXEC tSQLt.FakeTable 'eventuous.Streams', @Identity = 1;
END;
GO

CREATE PROCEDURE check_stream.[Test happy path Any]
AS
BEGIN
    DECLARE
        @stream_name      VARCHAR(850)     = 'Receipt-1',
        @expected_version INT              = -2, -- Any
        @current_version  INT,
        @stream_id        INT;

    EXEC eventuous.check_stream
        @stream_name      = @stream_name,
        @expected_version = @expected_version,
        @current_version  = @current_version,
        @stream_id        = @stream_id;

    /* Assert Streams */
    SELECT StreamName,
           [Version]
    INTO #ActualStreams
    FROM eventuous.Streams;

    SELECT TOP (0) *
    INTO #ExpectedStreams
    FROM #ActualStreams;

    INSERT INTO #ExpectedStreams (StreamName, [Version])
    VALUES (@stream_name, -1);

    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedStreams',  @Actual = N'#ActualStreams';

END;
GO

CREATE PROCEDURE check_stream.[Test happy path NoStream]
AS
BEGIN
    DECLARE
        @stream_name      VARCHAR(850)     = 'Receipt-1',
        @expected_version INT              = -1, -- NoStream
        @current_version  INT,
        @stream_id        INT;

    EXEC eventuous.check_stream
        @stream_name      = @stream_name,
        @expected_version = @expected_version,
        @current_version  = @current_version,
        @stream_id        = @stream_id;

    /* Assert Streams */
    SELECT StreamName,
           [Version]
    INTO #ActualStreams
    FROM eventuous.Streams;

    SELECT TOP (0) *
    INTO #ExpectedStreams
    FROM #ActualStreams;

    INSERT INTO #ExpectedStreams (StreamName, [Version])
    VALUES (@stream_name, -1);

    EXEC tSQLt.AssertEqualsTable @Expected = N'#ExpectedStreams',  @Actual = N'#ActualStreams';

END;
GO

CREATE PROCEDURE check_stream.[Test stream not found ExpectException]
AS
BEGIN
    DECLARE
        @stream_name      VARCHAR(850)     = 'Receipt-1',
        @expected_version INT              = -500, --trouble
        @current_version  INT,
        @stream_id        INT;

    DECLARE @ExpectedMessage VARCHAR(256) = 'StreamNotFound'
    EXEC tSQLt.ExpectException
        @ExpectedMessage  = @ExpectedMessage,
        @ExpectedSeverity = NULL,
        @ExpectedState    = NULL;

    EXEC eventuous.check_stream
        @stream_name      = @stream_name,
        @expected_version = @expected_version,
        @current_version  = @current_version,
        @stream_id        = @stream_id;
END;
GO

CREATE PROCEDURE check_stream.[Test stream exists wrong version ExpectExcepton]
AS
BEGIN
    DECLARE
        @stream_name      VARCHAR(850)     = 'Receipt-1',
        @expected_version INT              = 20, --trouble
        @current_version  INT              = 21, --trouble
        @stream_id        INT;

    INSERT INTO eventuous.Streams (StreamName, [Version])
    VALUES(
        @stream_name,
        @expected_version + 1 -- Trouble
    );

    DECLARE @ExpectedMessage VARCHAR(256) = CONCAT('WrongExpectedVersion ', @expected_version, ', current version ', @current_version)
    EXEC tSQLt.ExpectException
        @ExpectedMessage  = @ExpectedMessage,
        @ExpectedSeverity = NULL,
        @ExpectedState    = NULL;

    EXEC eventuous.check_stream
        @stream_name      = @stream_name,
        @expected_version = @expected_version,
        @current_version  = @current_version,
        @stream_id        = @stream_id;

END;
GO

-- EXEC tsqlt.RunTestClass 'check_stream' -- Don't check in