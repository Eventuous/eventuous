CREATE OR ALTER PROCEDURE __schema__.check_stream
    @stream_name NVARCHAR(850),
    @expected_version INT,
    @current_version INT OUTPUT,
    @stream_id INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @customErrorMessage NVARCHAR(200);

    -- UPDLOCK makes a concurrent append to the same stream wait for this transaction instead of reading the same version.
    SELECT
        @current_version = [Version],
        @stream_id = StreamId
    FROM [__schema__].Streams WITH (UPDLOCK)
    WHERE StreamName = @stream_name;

    IF @stream_id IS NULL
        BEGIN
            IF @expected_version = -2 -- Any
                OR @expected_version = -1 -- NoStream
                BEGIN
                    -- Two appends can both find the stream missing. UQ_StreamName decides which one creates it, with the
                    -- same collation that the lookup above uses, so names that differ only in case are one stream. The
                    -- other append waits for the first one to commit, gets a duplicate key error, and reads the stream.
                    -- Do not use HOLDLOCK or an application lock instead: HOLDLOCK locks the index range around the name,
                    -- which blocks unrelated streams, and a lock on the name does not follow the collation.
                    -- XACT_ABORT is off for the insert only, so that the duplicate key error does not doom the
                    -- transaction of the caller.
                    SET XACT_ABORT OFF;

                    BEGIN TRY
                        SET @current_version = -1
                        INSERT INTO [__schema__].Streams (
                            StreamName,
                            [Version]
                        ) VALUES (
                            @stream_name,
                            @current_version
                        );

                        SET @stream_id = SCOPE_IDENTITY();
                        SET XACT_ABORT ON;
                    END TRY
                    BEGIN CATCH
                        SET XACT_ABORT ON;

                        IF (ERROR_NUMBER() = 2627 OR ERROR_NUMBER() = 2601) AND (SELECT CHARINDEX(N'UQ_StreamName', ERROR_MESSAGE())) > 0
                            AND XACT_STATE() <> -1
                            BEGIN
                                SELECT
                                    @current_version = [Version],
                                    @stream_id = StreamId
                                FROM [__schema__].Streams WITH (UPDLOCK)
                                WHERE StreamName = @stream_name;
                            END;
                        ELSE
                        BEGIN
                            ;THROW;
                        END;
                    END CATCH;
                END;
            ELSE
            BEGIN
                ;THROW 50001, N'StreamNotFound', 1;
            END;
        END;

    -- A stream that another append created in the meantime gets the same check as a stream that already existed.
    IF @expected_version != -2 AND @expected_version != @current_version
        BEGIN
            SELECT @customErrorMessage = FORMATMESSAGE(N'WrongExpectedVersion %i, current version %i', @expected_version, @current_version);
            THROW 50000, @customErrorMessage, 1;
        END;
END;
