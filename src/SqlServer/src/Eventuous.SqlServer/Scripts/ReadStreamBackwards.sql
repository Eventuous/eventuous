CREATE OR ALTER PROCEDURE __schema__.read_stream_backwards
    @stream_name NVARCHAR(850),
    @count INT
AS
BEGIN

DECLARE @current_version int, @stream_id int

SELECT @current_version = Version, @stream_id = StreamId
FROM __schema__.Streams
WHERE StreamName = @stream_name

IF @stream_id IS NULL
    THROW 50001, 'StreamNotFound', 1;

SELECT TOP (@count) 
    MessageId, MessageType, StreamPosition, GlobalPosition,
    JsonData, JsonMetadata, Created
FROM __schema__.Messages
WHERE StreamId = @stream_id AND StreamPosition <= @current_version
ORDER BY Messages.GlobalPosition DESC

END