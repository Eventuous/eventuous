CREATE OR ALTER PROCEDURE __schema__.append_events
    @stream_name NVARCHAR(850),
    @expected_version INT,
    @created DATETIME2(7) NULL,
    @messages __schema__.StreamMessage READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @created IS NULL SET @created = SYSUTCDATETIME();
    IF NOT EXISTS (SELECT 1 FROM @messages)
        THROW 50002, N'No messages to append', 1;

    DECLARE
        @current_version INT,
        @stream_id INT,
        @position BIGINT,
        @count_messages INT,
        @new_version INT;

    -- capture inserted rows to compute final position
    DECLARE @inserted TABLE (
        GlobalPosition BIGINT
    );

    SELECT @count_messages = COUNT(1) FROM @messages;

    BEGIN TRY
        BEGIN TRANSACTION;

        EXEC __schema__.check_stream
            @stream_name       = @stream_name,
            @expected_version  = @expected_version,
            @current_version   = @current_version OUTPUT,
            @stream_id         = @stream_id OUTPUT;

        SET @new_version = @current_version + @count_messages;

        INSERT INTO __schema__.Messages (
            MessageId,
            MessageType,
            StreamId,
            StreamPosition,
            JsonData,
            JsonMetadata,
            Created
        )
        OUTPUT inserted.GlobalPosition
        INTO @inserted (GlobalPosition)
        SELECT
            message_id,
            message_type,
            @stream_id,
            @current_version + CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS INT),
            json_data,
            json_metadata,
            @created
        FROM @messages;

        -- If another writer raced us, the unique constraint (StreamId,StreamPosition) will throw here.
        -- Translate to WrongExpectedVersion.
        -- (Handled in the CATCH below.)

        UPDATE s
        SET s.[Version] = @new_version
        FROM __schema__.Streams s
        WHERE s.StreamId = @stream_id;

        -- final values to return
        SELECT @position = (
            SELECT MAX(GlobalPosition)
            FROM @inserted
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;

        DECLARE @errmsg  NVARCHAR(2048) = ERROR_MESSAGE();

        IF ERROR_NUMBER() IN (
            2627, -- Violation of PRIMARY KEY or UNIQUE constraint
            2601  -- Cannot insert duplicate key row in object with unique index
        )
           AND (
                @errmsg LIKE N'%UQ_StreamIdAndStreamPosition%'
           )
        BEGIN
            -- Must BEGIN with "WrongExpectedVersion" for the client detection of OptimisticConcurrencyException
            DECLARE @clientMsg NVARCHAR(4000) =
                N'WrongExpectedVersion: duplicate append for stream '
                + CAST(@stream_id AS NVARCHAR(20))
                + N' with expected_version=' + CAST(@expected_version AS NVARCHAR(20))
                + N'. SQL: ' + @errmsg;

            THROW 50000, @clientMsg, 1;
        END;

        ;THROW;
    END CATCH;

    SELECT
        @new_version current_version,
        @position position;
END;