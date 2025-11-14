CREATE OR ALTER PROCEDURE __schema__.read_stream_backwards
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
    FROM __schema__.Streams
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
    IF @from_position < 0
    BEGIN
        RETURN;
    END;

    -- If the starting position is greater than the current version, set it to the current version.
    IF @from_position > @current_version
    BEGIN
        SET @from_position = @current_version;
    END;

    SELECT TOP (@count)
        MessageId,
        MessageType,
        StreamPosition,
        GlobalPosition,
        JsonData,
        JsonMetadata,
        Created
    FROM __schema__.Messages
    WHERE StreamId = @stream_id
    AND StreamPosition <= @from_position
    ORDER BY StreamPosition DESC;
END;