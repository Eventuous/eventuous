EXEC tSQLt.NewTestClass 'Messages';
GO

CREATE PROCEDURE Messages.Setup
AS
BEGIN
    EXEC tSQLt.FakeTable 'eventuous.Messages', @Identity = 1;
END;
GO

CREATE PROCEDURE Messages.[Test StreamPosition is invalid json ExpectExcepton]
AS
BEGIN

    EXEC tSQLt.ApplyConstraint 'eventuous.Messages','CK_StreamPositionGteZero';

    DECLARE @ExpectedMessage VARCHAR(256) = 'The INSERT statement conflicted with the CHECK constraint "CK_StreamPositionGteZero". The conflict occurred in database "Eventuous", table "eventuous.Messages", column ''StreamPosition''.'
    EXEC tSQLt.ExpectException @ExpectedMessage  = @ExpectedMessage,
                               @ExpectedSeverity = NULL,
                               @ExpectedState    = NULL;
    INSERT INTO eventuous.Messages (StreamPosition)
    VALUES (-1);
END;
GO

CREATE PROCEDURE Messages.[Test CK_eventuous_Messages_JsonData violation ExpectExcepton]
AS
BEGIN

    EXEC tSQLt.ApplyConstraint 'eventuous.Messages','CK_JsonDataIsJson';

    DECLARE @ExpectedMessage VARCHAR(256) = 'The INSERT statement conflicted with the CHECK constraint "CK_JsonDataIsJson". The conflict occurred in database "Eventuous", table "eventuous.Messages", column ''JsonData''.'
    EXEC tSQLt.ExpectException @ExpectedMessage  = @ExpectedMessage,
                               @ExpectedSeverity = NULL,
                               @ExpectedState    = NULL;
    INSERT INTO eventuous.Messages (JsonData)
    VALUES (N'invalid');
END;
GO

CREATE PROCEDURE Messages.[Test CK_eventuous_Messages_JsonMetadata violation ExpectExcepton]
AS
BEGIN

    EXEC tSQLt.ApplyConstraint 'eventuous.Messages','CK_JsonMetadataIsJson';

    DECLARE @ExpectedMessage VARCHAR(256) = 'The INSERT statement conflicted with the CHECK constraint "CK_JsonMetadataIsJson". The conflict occurred in database "Eventuous", table "eventuous.Messages", column ''JsonMetadata''.'
    EXEC tSQLt.ExpectException @ExpectedMessage  = @ExpectedMessage,
                               @ExpectedSeverity = NULL,
                               @ExpectedState    = NULL;
    INSERT INTO eventuous.Messages (JsonMetadata)
    VALUES (N'invalid');
END;
GO

CREATE PROCEDURE Messages.[Test UQ_eventuous_Messages_StreamId_StreamPosition violoation ExpectExcepton]
AS
BEGIN

    EXEC tSQLt.ApplyConstraint 'eventuous.Messages','UQ_StreamIdAndStreamPosition';

    DECLARE @ExpectedMessage VARCHAR(256) = 'Violation of UNIQUE KEY constraint ''UQ_StreamIdAndStreamPosition''. Cannot insert duplicate key in object ''eventuous.Messages''. The duplicate key value is (1, 0).'
    EXEC tSQLt.ExpectException @ExpectedMessage  = @ExpectedMessage,
                               @ExpectedSeverity = NULL,
                               @ExpectedState    = NULL;
    INSERT INTO eventuous.Messages (StreamId, StreamPosition)
    VALUES (1, 0), (1, 0);
END;
GO

CREATE PROCEDURE Messages.[Test UQ_eventuous_Messages_StreamId_MessageId violation ExpectExcepton]
AS
BEGIN

    EXEC tSQLt.ApplyConstraint 'eventuous.Messages','UQ_StreamIdAndMessageId';

    DECLARE @messageId UNIQUEIDENTIFIER = NEWID();

    DECLARE @ExpectedMessage VARCHAR(256) = CONCAT('Violation of UNIQUE KEY constraint ''UQ_StreamIdAndMessageId''. Cannot insert duplicate key in object ''eventuous.Messages''. The duplicate key value is (1, ', @messageId, ').');
    EXEC tSQLt.ExpectException @ExpectedMessage  = @ExpectedMessage,
                               @ExpectedSeverity = NULL,
                               @ExpectedState    = NULL;
    INSERT INTO eventuous.Messages (StreamId, MessageId)
    VALUES (1, @messageId), (1, @messageId);
END;
GO

-- EXEC tsqlt.RunTestClass 'Messages' -- Don't check in