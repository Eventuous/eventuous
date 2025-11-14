CREATE TABLE eventuous.Messages (
    MessageId      UNIQUEIDENTIFIER      NOT NULL,
    MessageType    NVARCHAR(128)         NOT NULL,
    StreamId       INT                   NOT NULL,
    StreamPosition INT                   NOT NULL,
    GlobalPosition BIGINT IDENTITY (0,1) NOT NULL,
    JsonData       NVARCHAR(MAX)         NOT NULL,
    JsonMetadata   NVARCHAR(MAX)         NOT NULL,
    Created        DATETIME2(7)          NOT NULL,
    CONSTRAINT PK_Events PRIMARY KEY CLUSTERED (GlobalPosition) WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON),
    CONSTRAINT FK_MessageStreamId FOREIGN KEY (StreamId) REFERENCES eventuous.Streams (StreamId),
    CONSTRAINT UQ_StreamIdAndStreamPosition UNIQUE NONCLUSTERED (StreamId, StreamPosition),
    CONSTRAINT UQ_StreamIdAndMessageId UNIQUE NONCLUSTERED (StreamId, MessageId),
    CONSTRAINT CK_StreamPositionGteZero CHECK (StreamPosition >= 0),
    CONSTRAINT CK_JsonDataIsJson CHECK (ISJSON(JsonData) = 1),
    CONSTRAINT CK_JsonMetadataIsJson CHECK (ISJSON(JsonMetadata) = 1),
    INDEX IDX_EventsStream (StreamId)
);
GO