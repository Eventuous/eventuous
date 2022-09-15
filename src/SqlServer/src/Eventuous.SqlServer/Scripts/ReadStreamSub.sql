CREATE OR ALTER PROCEDURE __schema__.read_stream_sub
    @stream_id int,
    @from_position bigint,
    @count int
    AS
BEGIN
SELECT TOP (@count) m.message_id, m.message_type, m.stream_position,m.global_position, m.json_data,m.json_metadata,m.created
FROM __schema__.Messages m
WHERE m.stream_id = @stream_id AND m.stream_position >= @from_position
END