EXEC tSQLt.NewTestClass 'Streams';
GO

CREATE PROCEDURE Streams.Setup
AS
BEGIN
    EXEC tSQLt.FakeTable 'eventuous.Streams', @Identity = 1;
END;
GO

CREATE PROCEDURE Streams.[Test CK_eventuous_Streams_Version violation ExpectExcepton]
AS
BEGIN

    EXEC tSQLt.ApplyConstraint 'eventuous.Streams','CK_VersionGteNegativeOne';

    DECLARE @ExpectedMessage VARCHAR(256) = 'The INSERT statement conflicted with the CHECK constraint "CK_VersionGteNegativeOne". The conflict occurred in database "Eventuous", table "eventuous.Streams", column ''Version''.';
    EXEC tSQLt.ExpectException @ExpectedMessage  = @ExpectedMessage,
                               @ExpectedSeverity = NULL,
                               @ExpectedState    = NULL;
    INSERT INTO eventuous.Streams ([Version])
    VALUES (-2);
END;
GO

-- EXEC tsqlt.RunTestClass 'Streams' -- Don't check in
