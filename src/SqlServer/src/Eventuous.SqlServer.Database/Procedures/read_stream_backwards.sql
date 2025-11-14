CREATE PROCEDURE eventuous.read_stream_backwards
    @stream_name NVARCHAR(850),
    @from_position INT,
    @count INT
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

    -- nothing to read / invalid request
    IF @count <= 0
    BEGIN
        RETURN;
    END;

    -- Validate the starting position for backwards read.
    IF @from_position < 0                -- A negative starting position is invalid
    OR @from_position > @current_version -- A starting position greater than the current version means we're trying to read from beyond the head of the stream
    BEGIN
        RETURN;
    END;

    SELECT TOP (@count)
        MessageId,
        MessageType,
        StreamPosition,
        GlobalPosition,
        JsonData,
        JsonMetadata,
        Created
    FROM eventuous.Messages
    WHERE StreamId = @stream_id
    AND StreamPosition <= @from_position
    ORDER BY StreamPosition DESC;
END;