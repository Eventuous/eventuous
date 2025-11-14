CREATE TABLE eventuous.Streams (
    StreamId   INT IDENTITY (1,1) NOT NULL,
    StreamName NVARCHAR(850)      NOT NULL,
    [Version]  INT DEFAULT (-1)   NOT NULL,
    CONSTRAINT PK_Streams PRIMARY KEY CLUSTERED (StreamId) WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON),
    CONSTRAINT UQ_StreamName UNIQUE NONCLUSTERED (StreamName),
    CONSTRAINT CK_VersionGteNegativeOne CHECK ([Version] >= -1)
);
GO