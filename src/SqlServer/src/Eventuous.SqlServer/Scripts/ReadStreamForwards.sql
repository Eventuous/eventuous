CREATE OR ALTER PROCEDURE __schema__.read_stream_forwards
    @stream_name NVARCHAR(1000),
    @from_position INT,
    @count INT
AS
BEGIN

DECLARE @current_version int, @stream_id int

SELECT @current_version = Version, @stream_id = StreamId
FROM __schema__.Streams
WHERE StreamName = @stream_name

IF @stream_id IS NULL
    THROW 50001, 'StreamNotFound', 1;

IF @current_version < @from_position
	RETURN

SELECT TOP (@count) 
    MessageId, MessageType, StreamPosition, GlobalPosition,
    JsonData, JsonMetadata, Created
FROM __schema__.Messages
WHERE StreamId = @stream_id AND StreamPosition >= @from_position


END