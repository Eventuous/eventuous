CREATE OR ALTER PROCEDURE __schema__.append_events
    @stream_name varchar(1000),
    @expected_version int,
    @created datetime2 NULL,
    @messages __schema__.stream_message READONLY
AS
BEGIN

    declare    @current_version int,
        @stream_id int,
        @position bigint



    if @created is null
        BEGIN
            SET @created = SYSUTCDATETIME()
        END

    EXEC	[__schema__].[check_stream]	@stream_name, @expected_version, @current_version = @current_version OUTPUT,@stream_id = @stream_id OUTPUT

    INSERT INTO __schema__.Messages (message_id, message_type, stream_id, stream_position, json_data, json_metadata, created)
    SELECT  m.message_id, m.message_type, @stream_id, @current_version + (row_number()  OVER(ORDER BY (SELECT NULL))), m.json_data, m.json_metadata, @created -- OVER(ORDER BY (SELECT NULL))  may not garuantee order
    FROM @messages m

    SELECT TOP 1 @current_version =  m.stream_position, @position = m.global_position
    FROM __schema__.Messages m
    WHERE m.stream_id = @stream_id
    ORDER BY m.global_position DESC

    UPDATE __schema__.Streams SET version = @current_version WHERE stream_id = @stream_id

    SELECT @current_version AS current_version, @position AS position
END