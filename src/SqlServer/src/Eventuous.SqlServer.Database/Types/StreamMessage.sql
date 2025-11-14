CREATE type eventuous.StreamMessage AS TABLE (
    message_id    UNIQUEIDENTIFIER NOT NULL,
    message_type  NVARCHAR(128)    NOT NULL,
    json_data     NVARCHAR(MAX)    NOT NULL,
    json_metadata NVARCHAR(MAX)    NOT NULL
);
GO