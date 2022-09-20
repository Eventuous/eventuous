CREATE OR ALTER PROCEDURE __schema__.append_events
    @stream_name VARCHAR(1000),
    @expected_version INT,
    @created DATETIME2 NULL,
    @messages __schema__.StreamMessage READONLY
AS
BEGIN
    DECLARE @current_version INT,
        @stream_id INT,
        @position BIGINT

    if @created is null
        BEGIN
            SET @created = SYSUTCDATETIME()
        END

    EXEC [__schema__].[check_stream] @stream_name, @expected_version, @current_version = @current_version OUTPUT, @stream_id = @stream_id OUTPUT

    INSERT INTO __schema__.Messages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
    SELECT message_id, message_type, @stream_id, @current_version + (ROW_NUMBER() OVER(ORDER BY (SELECT NULL))), json_data, json_metadata, @created
    FROM @messages

    SELECT TOP 1 @current_version =  StreamPosition, @position = GlobalPosition
    FROM __schema__.Messages
    WHERE StreamId = @stream_id
    ORDER BY GlobalPosition DESC

    UPDATE __schema__.Streams SET Version = @current_version WHERE StreamId = @stream_id

    SELECT @current_version AS current_version, @position AS position
END