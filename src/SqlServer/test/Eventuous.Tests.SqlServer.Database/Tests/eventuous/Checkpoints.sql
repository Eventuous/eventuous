EXEC tSQLt.NewTestClass 'Checkpoints';
GO

CREATE PROCEDURE Checkpoints.Setup
AS
BEGIN
    EXEC tSQLt.FakeTable 'eventuous.Checkpoints';
END;
GO

CREATE PROCEDURE Checkpoints.[Test UQ_eventuous_Checkpoints_Id violation ExpectExcepton]
AS
BEGIN

    EXEC tSQLt.ApplyConstraint 'eventuous.Checkpoints','PK_Checkpoints';

    DECLARE @ExpectedMessage VARCHAR(256) = 'Violation of PRIMARY KEY constraint ''PK_Checkpoints''. Cannot insert duplicate key in object ''eventuous.Checkpoints''. The duplicate key value is (1).';
    EXEC tSQLt.ExpectException @ExpectedMessage  = @ExpectedMessage,
                               @ExpectedSeverity = NULL,
                               @ExpectedState    = NULL;
    INSERT INTO eventuous.Checkpoints (Id)
    VALUES (1), (1);
END;
GO

-- EXEC tsqlt.RunTestClass 'Checkpoints' -- Don't check in
