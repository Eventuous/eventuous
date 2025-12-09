IF (SCHEMA_ID(N'__schema__') IS NULL)
    BEGIN
        EXEC ('CREATE SCHEMA [__schema__] AUTHORIZATION [dbo]')
    END

IF OBJECT_ID('__schema__.snapshots', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.snapshots
        (
            stream_name NVARCHAR(1000) NOT NULL,
            revision    BIGINT         NOT NULL,
            event_type  NVARCHAR(128)  NOT NULL,
            json_data   NVARCHAR(MAX)  NOT NULL,
            created     DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
            CONSTRAINT PK_Snapshots PRIMARY KEY CLUSTERED (stream_name),
            CONSTRAINT CK_Snapshots_RevisionGteZero CHECK (revision >= 0),
            CONSTRAINT CK_Snapshots_JsonDataIsJson CHECK (ISJSON(json_data) = 1)
        );

        CREATE INDEX IDX_SnapshotsStreamName ON __schema__.snapshots (stream_name);
    END

