CREATE OR ALTER PROCEDURE __schema__.read_all_forwards
    @from_position bigint,
    @count int
    AS
BEGIN
SELECT TOP (@count) m.message_id, m.message_type, 
                    m.stream_position, m.global_position,
                    m.json_data, m.json_metadata, m.created,
                    s.stream_name
FROM __schema__.Messages m
         INNER JOIN __schema__.Streams s on s.stream_id = m.stream_id
WHERE m.global_position >= @from_position


END