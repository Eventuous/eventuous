CREATE OR ALTER PROCEDURE __schema__.check_stream
    @stream_name NVARCHAR(1000),
    @expected_version int,
    @current_version INT OUTPUT,
    @stream_id INT OUTPUT
    AS
BEGIN

SELECT @current_version = [Version], @stream_id =StreamId
    FROM __schema__.Streams 
    WHERE StreamName = @stream_name

IF @stream_id is null
BEGIN
    IF @expected_version = -2 --Any
    OR @expected_version = -1 -- NoStream
        BEGIN
            INSERT INTO __schema__.Streams (StreamName, Version) VALUES (@stream_name, -1);
            SELECT @current_version = Version, @stream_id = StreamId
                FROM __schema__.Streams
                WHERE StreamName = @stream_name
        END
    ELSE
        THROW 50001, 'StreamNotFound', 1;
END
    ELSE IF @expected_version != -2 and @expected_version != @current_version
        THROW 50000, 'WrongExpectedVersion %, current version %', 1;

END
