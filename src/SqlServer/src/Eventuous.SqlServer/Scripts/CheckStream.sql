CREATE OR ALTER PROCEDURE __schema__.check_stream
    -- Add the parameters for the stored procedure here
    @stream_name NVARCHAR(1000) ,
    @expected_version int,
    @current_version INT OUTPUT,
    @stream_id INT OUTPUT
    AS
BEGIN

SELECT @current_version = s.version, @stream_id =s.stream_id
FROM __schema__.streams s
WHERE s.stream_name = @stream_name
    IF @stream_id is null
BEGIN
IF @expected_version = -2 --Any
OR @expected_version = -1 -- NoStream
BEGIN
INSERT INTO __schema__.Streams (stream_name, version) VALUES (@stream_name,-1);
SELECT @current_version = s.version, @stream_id =s.stream_id
FROM __schema__.streams s
WHERE s.stream_name = @stream_name
END
    ELSE
BEGIN
THROW 50001, 'StreamNotFound', 1;
END
END
    ELSE IF @expected_version != -2 and @expected_version != @current_version
BEGIN
THROW 50000, 'WrongExpectedVersion %, current version %', 1;
END

END
