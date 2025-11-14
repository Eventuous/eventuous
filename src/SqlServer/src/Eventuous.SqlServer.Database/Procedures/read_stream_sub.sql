CREATE PROCEDURE eventuous.read_stream_sub
    @stream_id INT,
    @stream_name NVARCHAR(850),
    @from_position INT,
    @count INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SELECT TOP (@count)
        MessageId,
        MessageType,
        StreamPosition,
        GlobalPosition,
        JsonData,
        JsonMetadata,
        Created,
        @stream_name StreamName
    FROM eventuous.Messages
    WHERE StreamId = @stream_id
    AND StreamPosition >= @from_position
    ORDER BY GlobalPosition;
END;