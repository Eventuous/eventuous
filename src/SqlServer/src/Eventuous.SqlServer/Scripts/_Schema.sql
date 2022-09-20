IF (SCHEMA_ID(N'__schema__') IS NULL)
    BEGIN
        EXEC ('CREATE SCHEMA [__schema__] AUTHORIZATION [dbo]')
    END

IF OBJECT_ID('__schema__.Streams', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Streams
        (
            StreamId   INT IDENTITY (1,1) NOT NULL,
            StreamName NVARCHAR(1000)     NOT NULL,
            [Version]  INT DEFAULT (-1)   NOT NULL,
            CONSTRAINT PK_Streams PRIMARY KEY CLUSTERED (StreamId),
            CONSTRAINT UQ_StreamName UNIQUE NONCLUSTERED (StreamName),
            CONSTRAINT CK_VersionGteNegativeOne CHECK ([version] >= -1)
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
            JsonData       NVARCHAR(max)         NOT NULL,
            JsonMetadata   NVARCHAR(max),
            Created        DATETIME2,
            CONSTRAINT PK_Events PRIMARY KEY CLUSTERED (GlobalPosition),
            CONSTRAINT FK_MessageStreamId FOREIGN KEY (StreamId) REFERENCES __schema__.Streams (StreamId),
            CONSTRAINT UQ_StreamIdAndStreamPosition UNIQUE NONCLUSTERED (StreamId, StreamPosition),
            CONSTRAINT UQ_StreamIdAndMessageId UNIQUE NONCLUSTERED (StreamId, MessageId),
            CONSTRAINT CK_StreamPositionGteZero CHECK (Messages.StreamPosition >= 0),
            INDEX IDX_EventsStream (StreamId)
        );
    END

IF OBJECT_ID('__schema__.Checkpoints', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Checkpoints
        (
            Id       NVARCHAR(128) NOT NULL,
            Position BIGINT        NULL,
            CONSTRAINT PK_Checkpoints PRIMARY KEY CLUSTERED (Id),
        );
    END

IF TYPE_ID('__schema__.stream_message') IS NULL
    BEGIN
        CREATE type __schema__.StreamMessage AS TABLE
        (
            message_id    UNIQUEIDENTIFIER NOT NULL,
            message_type  NVARCHAR(128)    NOT NULL,
            json_data     NVARCHAR(max)    NOT NULL,
            json_metadata NVARCHAR(max)
        )
    END

