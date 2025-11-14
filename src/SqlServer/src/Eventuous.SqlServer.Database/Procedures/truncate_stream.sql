CREATE PROCEDURE eventuous.truncate_stream
    @stream_name NVARCHAR(850),
    @expected_version INT,
    @position INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @current_version INT,
        @stream_id INT;

    SELECT
        @current_version = [Version],
        @stream_id = StreamId
    FROM eventuous.Streams
    WHERE StreamName = @stream_name;

    IF @stream_id IS NULL
    BEGIN
        ;THROW 50001, 'StreamNotFound', 1;
    END;

    IF @current_version < @position
    BEGIN
        RETURN;
    END;

    IF @expected_version != -2 AND @expected_version != @current_version
    BEGIN
        DECLARE @customMessage NVARCHAR(4000);
        SELECT @customMessage = FORMATMESSAGE(N'WrongExpectedVersion %i, current version %i', @expected_version, @current_version);
        ;THROW 50000, @customMessage, 1;
    END;

    DELETE m
    FROM eventuous.Messages m
    WHERE m.StreamId = @stream_id
    AND m.StreamPosition < @position;
END;