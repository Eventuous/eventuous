IF (SCHEMA_ID(N'__schema__') IS NULL)
    BEGIN
        EXEC ('CREATE SCHEMA [__schema__] AUTHORIZATION [dbo]')
    END

IF OBJECT_ID('__schema__.Streams', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Streams
        (
            StreamId   INT IDENTITY (1,1) NOT NULL,
            StreamName NVARCHAR(850)      NOT NULL,
            [Version]  INT DEFAULT (-1)   NOT NULL,
            CONSTRAINT PK_Streams PRIMARY KEY CLUSTERED (StreamId) WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON),
            CONSTRAINT UQ_StreamName UNIQUE NONCLUSTERED (StreamName),
            CONSTRAINT CK_VersionGteNegativeOne CHECK ([Version] >= -1)
        );
    END

IF OBJECT_ID('__schema__.Messages', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Messages
        (
            MessageId      UNIQUEIDENTIFIER      NOT NULL,
            MessageType    NVARCHAR(128)         NOT NULL,
            StreamId       INT                   NOT NULL,
            StreamPosition INT                   NOT NULL,
            GlobalPosition BIGINT IDENTITY (0,1) NOT NULL,
            JsonData       NVARCHAR(MAX)         NOT NULL,
            JsonMetadata   NVARCHAR(MAX)         NOT NULL,
            Created        DATETIME2(7)          NOT NULL,
            CONSTRAINT PK_Events PRIMARY KEY CLUSTERED (GlobalPosition) WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON),
            CONSTRAINT FK_MessageStreamId FOREIGN KEY (StreamId) REFERENCES __schema__.Streams (StreamId),
            CONSTRAINT UQ_StreamIdAndStreamPosition UNIQUE NONCLUSTERED (StreamId, StreamPosition),
            CONSTRAINT UQ_StreamIdAndMessageId UNIQUE NONCLUSTERED (StreamId, MessageId),
            CONSTRAINT CK_StreamPositionGteZero CHECK (StreamPosition >= 0),
            CONSTRAINT CK_JsonDataIsJson CHECK (ISJSON(JsonData) = 1),
            CONSTRAINT CK_JsonMetadataIsJson CHECK (ISJSON(JsonMetadata) = 1),
            INDEX IDX_EventsStream (StreamId)
        );
    END

IF OBJECT_ID('__schema__.Checkpoints', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Checkpoints
        (
            Id       NVARCHAR(128) NOT NULL,
            Position BIGINT            NULL,
            CONSTRAINT PK_Checkpoints PRIMARY KEY CLUSTERED (Id),
        );
    END

IF TYPE_ID('__schema__.StreamMessage') IS NULL
    BEGIN
        CREATE type __schema__.StreamMessage AS TABLE
        (
            message_id    UNIQUEIDENTIFIER NOT NULL,
            message_type  NVARCHAR(128)    NOT NULL,
            json_data     NVARCHAR(MAX)    NOT NULL,
            json_metadata NVARCHAR(MAX)    NOT NULL
        )
    END
