CREATE OR ALTER PROCEDURE __schema__.read_stream_forwards
    @stream_name nvarchar(1000),
    @from_position int,
    @count int
    AS
BEGIN

 DECLARE @current_version int, @stream_id int

SELECT @current_version = s.version, @stream_id = s.stream_id
FROM __schema__.Streams s
WHERE s.stream_name = @stream_name


    IF @stream_id is null
BEGIN
	THROW 50001, 'StreamNotFound', 1;
END

 IF @current_version < @from_position
BEGIN
	RETURN
END


SELECT TOP (@count) m.message_id, m.message_type,
       m.stream_position, m.global_position,
       m.json_data, m.json_metadata, m.created
FROM __schema__.Messages m
WHERE m.stream_id = @stream_id AND m.stream_position >= @from_position


END