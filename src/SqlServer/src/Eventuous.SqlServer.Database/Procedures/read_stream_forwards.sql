CREATE PROCEDURE eventuous.read_stream_forwards
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

    IF @current_version < @from_position
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
    AND StreamPosition >= @from_position
    ORDER BY StreamPosition;

END;