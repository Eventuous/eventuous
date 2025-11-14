CREATE PROCEDURE eventuous.check_stream
    @stream_name NVARCHAR(850),
    @expected_version INT,
    @current_version INT OUTPUT,
    @stream_id INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @customErrorMessage NVARCHAR(200);

    SELECT
        @current_version = [Version],
        @stream_id = StreamId
    FROM eventuous.Streams
    WHERE StreamName = @stream_name;

    IF @stream_id IS NULL
        BEGIN
            IF @expected_version = -2 -- Any
                OR @expected_version = -1 -- NoStream
                BEGIN
                    BEGIN TRY
                        SET @current_version = -1;
                        INSERT INTO eventuous.Streams (
                            StreamName,
                            [Version]
                        ) VALUES (
                            @stream_name,
                            @current_version
                        );

                        SET @stream_id = SCOPE_IDENTITY();
                    END TRY
                    BEGIN CATCH
                        IF (ERROR_NUMBER() = 2627 OR ERROR_NUMBER() = 2601) AND (SELECT CHARINDEX(N'UQ_StreamName', ERROR_MESSAGE())) > 0
                            BEGIN
                                SELECT @customErrorMessage = FORMATMESSAGE(N'WrongExpectedVersion %i, stream already exists', @expected_version);
                                THROW 50000, @customErrorMessage, 1;
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
        END
    ELSE
    BEGIN
        IF @expected_version != -2 AND @expected_version != @current_version
            BEGIN
                SELECT @customErrorMessage = FORMATMESSAGE(N'WrongExpectedVersion %i, current version %i', @expected_version, @current_version);
                THROW 50000, @customErrorMessage, 1;
            END;
    END;
END;