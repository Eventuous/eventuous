CREATE OR ALTER PROCEDURE __schema__.append_events
    @stream_name VARCHAR(850),
    @expected_version INT,
    @created DATETIME2 NULL,
    @messages __schema__.StreamMessage READONLY
AS
BEGIN
    DECLARE @current_version INT,
        @stream_id INT,
        @position BIGINT,
        @customErrorMessage NVARCHAR(200),
        @newMessagesCount INT,
        @expected_StreamVersionAfterUpdate INT,
        @actual_StreamVersionAfterUpdate INT

    if @created is null
        BEGIN
            SET @created = SYSUTCDATETIME()
        END

    EXEC [__schema__].[check_stream] @stream_name, @expected_version, @current_version = @current_version OUTPUT, @stream_id = @stream_id OUTPUT

    BEGIN TRY
        INSERT INTO __schema__.Messages (MessageId, MessageType, StreamId, StreamPosition, JsonData, JsonMetadata, Created)
        SELECT message_id, message_type, @stream_id, @current_version + (ROW_NUMBER() OVER(ORDER BY (SELECT NULL))), json_data, json_metadata, @created
        FROM @messages
    END TRY
    BEGIN CATCH
        IF (ERROR_NUMBER() = 2627 OR ERROR_NUMBER() = 2601) AND (SELECT CHARINDEX(N'UQ_StreamIdAndStreamPosition', ERROR_MESSAGE())) > 0
            BEGIN
                DECLARE @streamIdFromError nvarchar(20) = SUBSTRING(ERROR_MESSAGE(), PATINDEX(N'%[0-9]%,%', ERROR_MESSAGE()), PATINDEX(N'%, [0-9]%).', ERROR_MESSAGE()) - PATINDEX(N'%[0-9]%,%', ERROR_MESSAGE()))
                DECLARE @streamPositionFromError nvarchar(20) = SUBSTRING(ERROR_MESSAGE(), (PATINDEX(N'%, [0-9]%).', ERROR_MESSAGE())) + 2, PATINDEX(N'%).', ERROR_MESSAGE()) - (PATINDEX(N'%, [0-9]%).', ERROR_MESSAGE()) + 2))

                -- TODO: There are multiple causes of OptimisticConcurrencyExceptions, but current client code is hard-coded to check for 'WrongExpectedVersion' in message and 50000 as error number.
                SELECT @customErrorMessage = FORMATMESSAGE(N'WrongExpectedVersion, another message has already been written at stream position %s on stream %s.', @streamIdFromError, @streamPositionFromError);
                THROW 50000, @customErrorMessage, 1;
            END
        ELSE
            THROW
    END CATCH
    
    SELECT TOP 1 @current_version =  StreamPosition, @position = GlobalPosition
    FROM __schema__.Messages
    WHERE StreamId = @stream_id
    ORDER BY GlobalPosition DESC

    UPDATE __schema__.Streams SET Version = @current_version WHERE StreamId = @stream_id

    SELECT @current_version AS current_version, @position AS position
END