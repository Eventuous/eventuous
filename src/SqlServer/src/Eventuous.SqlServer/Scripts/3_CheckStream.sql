CREATE OR ALTER PROCEDURE __schema__.check_stream @stream_name NVARCHAR(850),
                                                  @expected_version int,
                                                  @current_version INT OUTPUT,
                                                  @stream_id INT OUTPUT
AS
BEGIN
    DECLARE @customErrorMessage NVARCHAR(200)

    SELECT @current_version = [Version], @stream_id = StreamId
    FROM [__schema__].Streams
    WHERE StreamName = @stream_name

    IF @stream_id is null
        BEGIN
            IF @expected_version = -2 -- Any
                OR @expected_version = -1 -- NoStream
                BEGIN
                    BEGIN TRY
                        INSERT INTO [__schema__].Streams (StreamName, Version) VALUES (@stream_name, -1);
                        SELECT @current_version = Version, @stream_id = StreamId
                        FROM [__schema__].Streams
                        WHERE StreamName = @stream_name
                    END TRY
                    BEGIN CATCH
                        IF (ERROR_NUMBER() = 2627 OR ERROR_NUMBER() = 2601) AND (SELECT CHARINDEX(N'UQ_StreamName', ERROR_MESSAGE())) > 0
                            BEGIN
                                SELECT @customErrorMessage = FORMATMESSAGE(N'WrongExpectedVersion %i, stream already exists', @expected_version);
                                THROW 50000, @customErrorMessage, 1;
                            END
                        ELSE
                            THROW
                    END CATCH
                END
            ELSE
                THROW 50001, N'StreamNotFound', 1;
        END
    ELSE
        IF @expected_version != -2 and @expected_version != @current_version
            BEGIN
                SELECT @customErrorMessage = FORMATMESSAGE(N'WrongExpectedVersion %i, current version %i', @expected_version, @current_version);
                THROW 50000, @customErrorMessage, 1;
            END
END
