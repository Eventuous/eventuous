IF (SCHEMA_ID(N'__schema__') IS NULL)
    BEGIN
        EXEC ('CREATE SCHEMA [__schema__] AUTHORIZATION [dbo]')
    END

IF OBJECT_ID('__schema__.Streams', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Streams
        (
            stream_name NVARCHAR(1000)     NOT NULL,
            stream_id   INT IDENTITY (1,1) NOT NULL,
            [version]   INT DEFAULT (-1)   NOT NULL,
            CONSTRAINT pk_streams PRIMARY KEY CLUSTERED (stream_id),
            CONSTRAINT uq_streams_name UNIQUE NONCLUSTERED (stream_name),
            CONSTRAINT ck_version_gte_negative_one CHECK ([version] >= -1)
        );
    END

IF object_id('__schema__.Messages', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.Messages
        (
            message_id      UNIQUEIDENTIFIER      NOT NULL,
            message_type    NVARCHAR(128)         NOT NULL,
            stream_id       INT                   NOT NULL,
            stream_position INT                   NOT NULL,
            global_position BIGINT IDENTITY (0,1) NOT NULL,
            json_data       NVARCHAR(max)         NOT NULL,
            json_metadata   NVARCHAR(max),
            created         DATETIME2,
            CONSTRAINT pk_events PRIMARY KEY CLUSTERED (global_position),
            CONSTRAINT fk_messages_stream FOREIGN KEY (stream_id) REFERENCES __schema__.Streams (stream_id),
            CONSTRAINT uq_messages_stream_id_and_stream_position UNIQUE NONCLUSTERED (stream_id, stream_position),
            CONSTRAINT uq_stream_id_and_message_id UNIQUE NONCLUSTERED (stream_id, message_id),
            CONSTRAINT ck_stream_position_gte_zero CHECK (stream_position >= 0),
            INDEX events_stream_idx (stream_id)
        );
    END

IF object_id('__schema__.checkpoints', 'U') IS NULL
    BEGIN
        CREATE TABLE __schema__.checkpoints
        (
            id       NVARCHAR(128) NOT NULL,
            position BIGINT        NULL,
            CONSTRAINT pk_checkpoints PRIMARY KEY CLUSTERED (id),
        );
    END

IF TYPE_ID('__schema__.stream_message') IS NULL
    BEGIN
        CREATE type __schema__.stream_message AS TABLE
        (
            message_id    UNIQUEIDENTIFIER NOT NULL,
            message_type  NVARCHAR(128)    NOT NULL,
            json_data     NVARCHAR(max)    NOT NULL,
            json_metadata NVARCHAR(max)
        )
    END

