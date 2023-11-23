CREATE OR ALTER PROCEDURE __schema__.read_stream_sub
    @stream_id int,
    @stream_name NVARCHAR(850),
    @from_position bigint,
    @count int
    AS
BEGIN
    
SELECT TOP (@count) 
    MessageId, MessageType, StreamPosition, GlobalPosition,
    JsonData, JsonMetadata, Created, @stream_name AS StreamName
FROM __schema__.Messages
WHERE StreamId = @stream_id AND Messages.StreamPosition >= @from_position
ORDER BY Messages.GlobalPosition
    
END