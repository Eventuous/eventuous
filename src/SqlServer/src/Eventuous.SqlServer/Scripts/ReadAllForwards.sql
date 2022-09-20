CREATE OR ALTER PROCEDURE __schema__.read_all_forwards
    @from_position bigint,
    @count int
    AS
BEGIN
    
SELECT TOP (@count) 
    MessageId, MessageType, StreamPosition, GlobalPosition,
    JsonData, JsonMetadata, Created, StreamName
FROM __schema__.Messages
INNER JOIN __schema__.Streams ON Messages.StreamId = Streams.StreamId
WHERE Messages.GlobalPosition >= @from_position

END