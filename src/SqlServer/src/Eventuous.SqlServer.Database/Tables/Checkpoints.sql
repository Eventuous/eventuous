CREATE TABLE eventuous.Checkpoints (
    Id       NVARCHAR(128) NOT NULL,
    Position BIGINT            NULL,
    CONSTRAINT PK_Checkpoints PRIMARY KEY CLUSTERED (Id),
);
GO