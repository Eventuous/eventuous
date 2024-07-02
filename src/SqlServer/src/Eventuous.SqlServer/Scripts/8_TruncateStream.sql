CREATE OR ALTER PROCEDURE __schema__.truncate_stream
    @stream_name NVARCHAR(850),
    @expected_version INT,
    @position INT
AS
BEGIN

DECLARE @current_version int, @stream_id int

SELECT @current_version = Version, @stream_id = StreamId
FROM __schema__.Streams
WHERE StreamName = @stream_name

IF @stream_id IS NULL
    THROW 50001, 'StreamNotFound', 1;

IF @current_version < @position
	RETURN

IF @expected_version != -2 and @expected_version != @current_version
    THROW 50000, 'WrongExpectedVersion %, current version %', 1;

DELETE FROM __schema__.Messages
WHERE StreamId = @stream_id AND StreamPosition < @position

END